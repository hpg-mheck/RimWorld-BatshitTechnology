# Validates the mod's Defs against the real game, without launching it.
# A static stand-in for RimWorld's XML loader: every element must map to a
# real field (recursing into nested objects, honoring [LoadAlias]), enum
# values must parse, classes must resolve, Def references must exist in
# vanilla Data or the mod, and texture paths must exist in the mod's
# Textures/ or the game's texture list. Exits 1 when it finds problems.
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
$defFiles | % {
  try { $x = Load-Xml $_.FullName } catch { $unparsed++; return }
  foreach ($d in $x.DocumentElement.ChildNodes) {
    if ($d.NodeType -ne 'Element') { continue }
    $n = $d.SelectSingleNode('defName')
    if ($n) { $knownDefs["$($d.LocalName)/$($n.InnerText.Trim())"] = $true }
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
  $f = $type.GetField($name, $fieldFlags)
  if ($f) { return $f }
  return $type.GetFields($fieldFlags) | ? {
    $_.CustomAttributes | ? {
      $_.AttributeType.Name -eq 'LoadAliasAttribute' -and
        $_.ConstructorArguments[0].Value -eq $name
    }
  } | Select-Object -First 1
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

"problems: $script:problems"
exit [int]($script:problems -gt 0)
