# Validates the mod's Defs against the real game, without launching it.
# A static stand-in for RimWorld's XML loader: every element must map to a
# real field (recursing into nested objects, honoring [LoadAlias]), enum
# values must parse, classes must resolve, Def references must exist in
# vanilla Data or the mod, and texture paths must exist in the mod's
# Textures/ or the game's texture list. Patches/ too: each operation's
# class must be a PatchOperation with real fields, its XPath must select
# something in the defs as the game merges them (vanilla Data and the
# mod), and what it adds must load into the fields it lands in. Exits 1
# when it finds problems.
#
# Run with Windows PowerShell 5.1 (it reflects over .NET Framework DLLs),
# after building the mod:
#   powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools\validate-defs.ps1
# Defaults: the repository this script sits in, and $env:RIMWORLD_DIR.
param(
  [string]$ModRoot = (Split-Path $PSScriptRoot -Parent),
  [string]$RimWorld = $env:RIMWORLD_DIR
)
$ErrorActionPreference = 'Stop'
if (-not $RimWorld -or -not (Test-Path (Join-Path $RimWorld 'RimWorldWin64_Data'))) {
  throw "RimWorld not found at '$RimWorld'. Set RIMWORLD_DIR or pass -RimWorld."
}

$managed = Join-Path $RimWorld 'RimWorldWin64_Data\Managed'
$game = [Reflection.Assembly]::LoadFrom("$managed\Assembly-CSharp.dll")
$mod = [Reflection.Assembly]::LoadFrom(
  (Join-Path $ModRoot 'Assemblies\BatshitTechnology.dll'))
$gameTypes = try { $game.GetTypes() }
  catch [Reflection.ReflectionTypeLoadException] { $_.Exception.Types | ? { $_ } }
$modTypes = $mod.GetTypes()
$defBase = $game.GetType('Verse.Def')
$fieldFlags = [Reflection.BindingFlags]'Public,NonPublic,Instance'

<#
.SYNOPSIS
Finds a type by full name (mod first), or by short name in RimWorld's
namespaces, the way Def XML refers to classes.
#>
function Resolve-Type([string]$name) {
  foreach ($t in $modTypes + $gameTypes) {
    if ($t.FullName -eq $name) { return $t }
  }
  foreach ($t in $gameTypes) {
    if ($t.Name -eq $name -and $t.Namespace -in 'Verse', 'RimWorld', 'Verse.AI') {
      return $t
    }
  }
  return $null
}

<#
.SYNOPSIS
Loads an XML file with XmlDocument.Load, which honors its encoding.
#>
function Load-Xml([string]$path) {
  $x = New-Object Xml.XmlDocument
  $x.Load($path)
  return $x
}

# defNames declared in vanilla Data (Core + DLCs) and the mod, keyed
# "<XmlTag>/<defName>". (Use .LocalName: PowerShell's XML adapter returns
# a Name="..." attribute for .Name.)
$knownDefs = @{}
$unparsed = 0
$defFiles = @(Get-ChildItem (Join-Path $RimWorld 'Data') -Recurse -Filter *.xml |
    ? FullName -match '\\Defs\\') +
  @(Get-ChildItem (Join-Path $ModRoot 'Defs') -Recurse -Filter *.xml)

# The mod's patches, and the def types their XPaths start from
# ("Defs/ThinkTreeDef[...]"); an XPath that names none needs every def.
$patchDir = Join-Path $ModRoot 'Patches'
$patchFiles = @(if (Test-Path $patchDir) {
    Get-ChildItem $patchDir -Recurse -Filter *.xml })
$patchTags = @{}
$patchNeedsAll = $false
foreach ($pf in $patchFiles) {
  try { $px = Load-Xml $pf.FullName } catch { continue }
  foreach ($xp in $px.SelectNodes('//xpath')) {
    if ($xp.InnerText -match '^\s*/?\s*Defs\s*/\s*([A-Za-z_][\w.]*)') {
      $patchTags[$Matches[1]] = $true
    } else { $patchNeedsAll = $true }
  }
}

# The defs those patches can reach, merged under one <Defs> root as the
# game merges every mod's defs before it applies patches.
$merged = New-Object Xml.XmlDocument
$mergedRoot = $merged.AppendChild($merged.CreateElement('Defs'))

$defFiles | % {
  try { $x = Load-Xml $_.FullName } catch { $unparsed++; return }
  foreach ($d in $x.DocumentElement.ChildNodes) {
    if ($d.NodeType -ne 'Element') { continue }
    $n = $d.SelectSingleNode('defName')
    if ($n) { $knownDefs["$($d.LocalName)/$($n.InnerText.Trim())"] = $true }
    if ($patchNeedsAll -or $patchTags.ContainsKey($d.LocalName)) {
      [void]$mergedRoot.AppendChild($merged.ImportNode($d, $true))
    }
  }
}

$script:problems = 0

<#
.SYNOPSIS
Records one problem and prints it.
#>
function Fail([string]$msg) {
  $script:problems++
  "  ERROR $msg"
}

<#
.SYNOPSIS
Checks a Def reference exists. A def type's XML tag is its class name
(ThingDef, WorkTypeDef, ...); Def subclasses are rare enough to ignore.
#>
function Check-DefRef([Type]$defType, [string]$name, [string]$path) {
  if (-not $knownDefs.ContainsKey("$($defType.Name)/$name")) {
    Fail "$path references missing $($defType.Name) '$name'"
  }
}

<#
.SYNOPSIS
Checks one XML value against the field type it loads into.
#>
function Check-Value([Type]$ft, [Xml.XmlNode]$node, [string]$path) {
  $v = $node.InnerText.Trim()
  if ($ft.IsEnum) {
    try { [void][Enum]::Parse($ft, $v) }
    catch { Fail "$path = '$v' is not a $($ft.Name) value" }
  }
  elseif ($ft.FullName -eq 'System.Type') {
    if (-not (Resolve-Type $v)) { Fail "$path class '$v' not found" }
  }
  elseif ($defBase.IsAssignableFrom($ft)) { Check-DefRef $ft $v $path }
  elseif ($ft.IsGenericType -and $ft.Name -like 'List*') {
    $et = $ft.GetGenericArguments()[0]
    foreach ($li in $node.ChildNodes) {
      if ($li.NodeType -ne 'Element') { continue }
      $cls = $li.GetAttribute('Class')
      if ($cls) {
        $ct = Resolve-Type $cls
        if (-not $ct) { Fail "$path/li Class '$cls' not found"; continue }
        Check-Object $ct $li "$path/li[$cls]"
      }
      elseif ($et -eq [string] -and $node.LocalName -eq 'categories') {
        # ThingFilter stores category names as strings
        $categoryDef = $game.GetType('Verse.ThingCategoryDef')
        Check-DefRef $categoryDef $li.InnerText.Trim() "$path/li"
      }
      elseif ($et.Name -eq 'ThingDefCountClass' -and $li.LocalName -ne 'li') {
        # costList / products: <DefName>count</DefName>
        $thingDef = $game.GetType('Verse.ThingDef')
        Check-DefRef $thingDef $li.LocalName "$path/$($li.LocalName)"
      }
      else { Check-Value $et $li "$path/li" }
    }
  }
  elseif (-not $ft.IsPrimitive -and $ft -ne [string] -and -not $ft.IsValueType -and
      $node.SelectSingleNode('*')) {
    # Nested object such as <building> or <filter>. Like the game's loader
    # (DirectXmlToObject), a Class attribute picks a subclass of the field's
    # type, e.g. <detonation Class="BatshitTechnology.Detonation_Bomb">.
    $cls = $node.GetAttribute('Class')
    if ($cls) {
      $ct = Resolve-Type $cls
      if (-not $ct) { Fail "$path Class '$cls' not found"; return }
      if (-not $ft.IsAssignableFrom($ct)) {
        Fail "$path Class '$cls' is not a $($ft.Name)"; return
      }
      Check-Object $ct $node "$path[$cls]"
    }
    else { Check-Object $ft $node $path }
  }
}

<#
.SYNOPSIS
Finds the field an XML element loads into: by name, or via the
[LoadAlias("name")] attribute the loader also honors.
#>
function Find-Field([Type]$type, [string]$name) {
  # Walk up the hierarchy: GetField does not return a base class's private
  # fields (PatchOperation.success), but the game's loader sets them.
  for ($t = $type; $t; $t = $t.BaseType) {
    $f = $t.GetField($name, $fieldFlags)
    if ($f) { return $f }
    $f = $t.GetFields($fieldFlags) | ? {
      $_.CustomAttributes | ? {
        $_.AttributeType.Name -eq 'LoadAliasAttribute' -and
          $_.ConstructorArguments[0].Value -eq $name
      }
    } | Select-Object -First 1
    if ($f) { return $f }
  }
  return $null
}

<#
.SYNOPSIS
Checks every child element of an object node maps to a real field.
#>
function Check-Object([Type]$type, [Xml.XmlNode]$node, [string]$path) {
  foreach ($child in $node.ChildNodes) {
    if ($child.NodeType -ne 'Element') { continue }
    $f = Find-Field $type $child.LocalName
    if (-not $f) {
      Fail "$path/<$($child.LocalName)> is not a field of $($type.Name)"
      continue
    }
    Check-Value $f.FieldType $child "$path/$($child.LocalName)"
  }
}

"known defs indexed: $($knownDefs.Count) (unparsed files: $unparsed)"

# Texture paths: the mod's own Textures/ folder, or vanilla's (listed
# lowercase in globalgamemanagers).
$ggmPath = Join-Path $RimWorld 'RimWorldWin64_Data\globalgamemanagers'
$ggm = [IO.File]::ReadAllText($ggmPath, [Text.Encoding]::GetEncoding(28591))
$vanillaTextures = @{}
foreach ($m in [regex]::Matches($ggm, 'textures/[a-z0-9_/\-]+')) {
  $vanillaTextures[$m.Value] = $true
}

<#
.SYNOPSIS
Whether a texture path resolves: single, multi (_north) or folder graphics,
in the mod or in vanilla.
#>
function Texture-Exists([string]$p) {
  $modTex = Join-Path $ModRoot "Textures\$p"
  if ((Test-Path "$modTex.png") -or (Test-Path "${modTex}_north.png") -or
      (Test-Path $modTex -PathType Container)) {
    return $true
  }
  $v = "textures/$($p.ToLower())"
  return $vanillaTextures.ContainsKey($v) -or
    $vanillaTextures.ContainsKey("${v}_north") -or
    ($vanillaTextures.Keys | ? { $_.StartsWith("$v/") } | Select-Object -First 1)
}

$textureXPath = '//texPath | //texturePath | //fuelIconPath | //uiIconPath | //iconPath'
Get-ChildItem (Join-Path $ModRoot 'Defs') -Recurse -Filter *.xml | % {
  foreach ($n in (Load-Xml $_.FullName).SelectNodes($textureXPath)) {
    $p = $n.InnerText.Trim()
    if (-not (Texture-Exists $p)) {
      Fail "texture '$p' ($($n.LocalName)) not found in mod Textures/ or vanilla"
    }
  }
}

Get-ChildItem (Join-Path $ModRoot 'Defs') -Recurse -Filter *.xml | % {
  "$($_.FullName.Substring($ModRoot.Length + 1))"
  $x = Load-Xml $_.FullName
  foreach ($d in $x.DocumentElement.ChildNodes) {
    if ($d.NodeType -ne 'Element') { continue }
    $t = Resolve-Type $d.LocalName
    if (-not $t) { Fail "unknown def type $($d.LocalName)"; continue }
    Check-Object $t $d $d.SelectSingleNode('defName').InnerText
  }
}

$patchBase = $game.GetType('Verse.PatchOperation')
$patchAdd = $game.GetType('Verse.PatchOperationAdd')
$patchInsert = $game.GetType('Verse.PatchOperationInsert')

<#
.SYNOPSIS
The type a node of the merged defs loads into, found by walking down from
its def: a field's type, a list's element type for <li>, or the class a
Class attribute names. Null when the path leaves the known types.
#>
function Node-Type([Xml.XmlNode]$node) {
  if (-not $node -or $node -isnot [Xml.XmlElement]) { return $null }
  $parent = $node.ParentNode
  if ($parent -eq $mergedRoot) { return Resolve-Type $node.LocalName }
  $parentType = Node-Type $parent
  if (-not $parentType) { return $null }
  $cls = $node.GetAttribute('Class')
  if ($cls) { return Resolve-Type $cls }
  if ($node.LocalName -eq 'li') {
    if ($parentType.IsGenericType) {
      return $parentType.GetGenericArguments()[0]
    }
    return $null
  }
  $f = Find-Field $parentType $node.LocalName
  if ($f) { return $f.FieldType }
  return $null
}

<#
.SYNOPSIS
Checks the elements an Add or Insert puts into a container node: each must
load into it, a Class must name a subclass of what the container holds,
and its fields must be real.
#>
function Check-Added([Xml.XmlNode]$value, [Xml.XmlNode]$container,
    [string]$path) {
  $ct = Node-Type $container
  if (-not $ct) {
    Fail "$path adds to <$($container.LocalName)>, whose type is unknown"
    return
  }
  foreach ($child in $value.ChildNodes) {
    if ($child.NodeType -ne 'Element') { continue }
    if ($child.LocalName -eq 'li') {
      if (-not $ct.IsGenericType) {
        Fail "$path adds <li> to $($container.LocalName), not a list"
        continue
      }
      $et = $ct.GetGenericArguments()[0]
      $cls = $child.GetAttribute('Class')
      if ($cls) {
        $lt = Resolve-Type $cls
        if (-not $lt) { Fail "$path/li Class '$cls' not found"; continue }
        if (-not $et.IsAssignableFrom($lt)) {
          Fail "$path/li Class '$cls' is not a $($et.Name)"; continue
        }
        Check-Object $lt $child "$path/li[$cls]"
      } else { Check-Value $et $child "$path/li" }
    } else {
      $f = Find-Field $ct $child.LocalName
      if (-not $f) {
        Fail "$path/<$($child.LocalName)> is not a field of $($ct.Name)"
      } else { Check-Value $f.FieldType $child "$path/$($child.LocalName)" }
    }
  }
}

<#
.SYNOPSIS
Checks one patch operation: a PatchOperation class, real fields, nested
operations in turn, an XPath that parses and selects something (unless
its success mode says a miss is fine), and what an Add or Insert adds.
#>
function Check-Operation([Xml.XmlElement]$op, [string]$path) {
  $cls = $op.GetAttribute('Class')
  $t = if ($cls) { Resolve-Type $cls } else { $null }
  if (-not $t) { Fail "$path Class '$cls' not found"; return }
  if (-not $patchBase.IsAssignableFrom($t)) {
    Fail "$path Class '$cls' is not a PatchOperation"; return
  }
  $value = $null
  foreach ($child in $op.ChildNodes) {
    if ($child.NodeType -ne 'Element') { continue }
    $cp = "$path/$($child.LocalName)"
    $f = Find-Field $t $child.LocalName
    if (-not $f) {
      Fail "$path/<$($child.LocalName)> is not a field of $($t.Name)"
      continue
    }
    $ft = $f.FieldType
    if ($ft.Name -eq 'XmlContainer') { $value = $child }
    elseif ($child.LocalName -eq 'xpath') { }
    elseif ($patchBase.IsAssignableFrom($ft)) { Check-Operation $child $cp }
    elseif ($ft.IsGenericType -and
        $patchBase.IsAssignableFrom($ft.GetGenericArguments()[0])) {
      foreach ($li in $child.ChildNodes) {
        if ($li.NodeType -eq 'Element') { Check-Operation $li "$cp/li" }
      }
    }
    else { Check-Value $ft $child $cp }
  }
  $xpath = $op.SelectSingleNode('xpath')
  if (-not $xpath) { return }
  try { $targets = @($merged.SelectNodes($xpath.InnerText)) }
  catch { Fail "$path xpath does not parse: $($_.Exception.Message)"; return }
  $success = $op.SelectSingleNode('success')
  $mayMiss = $success -and $success.InnerText.Trim() -in 'Always', 'Invert'
  $shown = ($xpath.InnerText -replace '\s+', '')
  if ($targets.Count -eq 0 -and -not $mayMiss) {
    Fail "$path xpath selects nothing: $shown"
  }
  "  $($t.Name): $($targets.Count) match(es) for $shown"
  if (-not $value) { return }
  foreach ($target in $targets) {
    if ($patchAdd.IsAssignableFrom($t)) {
      Check-Added $value $target $path
    } elseif ($patchInsert.IsAssignableFrom($t)) {
      Check-Added $value $target.ParentNode $path
    }
  }
}

foreach ($pf in $patchFiles) {
  "$($pf.FullName.Substring($ModRoot.Length + 1))"
  try { $px = Load-Xml $pf.FullName }
  catch { Fail "$($pf.Name) does not parse: $($_.Exception.Message)"; continue }
  if ($px.DocumentElement.LocalName -ne 'Patch') {
    Fail "$($pf.Name) root is <$($px.DocumentElement.LocalName)>, not <Patch>"
    continue
  }
  $i = 0
  foreach ($op in $px.DocumentElement.ChildNodes) {
    if ($op.NodeType -ne 'Element') { continue }
    $i++
    if ($op.LocalName -ne 'Operation') {
      Fail "$($pf.Name) <$($op.LocalName)> is not an <Operation>"; continue
    }
    Check-Operation $op "$($pf.Name) operation $i"
  }
}

"problems: $script:problems"
exit [int]($script:problems -gt 0)
