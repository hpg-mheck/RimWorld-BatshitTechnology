// Batshit Technology - smoke checks for the Showcase API's held weather
// (slice 19000a; docs/SHOWCASE-API.md, "The Held Weather"). The directed
// test showcase holds Clear. Before it begins the smoke game's weather is
// set to fog; the checks see the hold replace it at once, undo another
// weather on the next tick and keep the weather's age under the
// decider's turn, stand aside while a scene has borrowed the weather and
// take it back when that scene stops running, and give the fog back when
// the showcase finishes and when it is stopped. The smoke game's own
// weather is put back after each run.

using BatshitTechnology.Showcases;
using RimWorld;
using Verse;

namespace BatshitTechnology
{
    /// <summary>The smoke runner's held-weather checks.</summary>
    public partial class GameComponent_SmokeTest
    {
        /// <summary>
        /// Notes the smoke game's weather and sets another at once (both
        /// the current and the last weather), for a held-weather test.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="defName">The weather to set, such as Fog.</param>
        /// <returns>The weather set, or null if there is no such weather.</returns>
        private WeatherDef SetSmokeWeather(Map map, string defName)
        {
            WeatherManager weather = map?.weatherManager;
            WeatherDef def = DefDatabase<WeatherDef>.GetNamedSilentFail(defName);
            if (weather == null || def == null)
            {
                return null;
            }

            smokeWeather = (weather.curWeather, weather.lastWeather, weather.curWeatherAge);
            weather.curWeather = def;
            weather.lastWeather = def;
            weather.curWeatherAge = 0;
            return def;
        }

        /// <summary>
        /// Puts back the smoke game's weather from before the last
        /// <see cref="SetSmokeWeather"/>, if one was noted.
        /// </summary>
        /// <param name="map">The map.</param>
        private void PutBackSmokeWeather(Map map)
        {
            WeatherManager weather = map?.weatherManager;
            if (weather == null || smokeWeather.Current == null || smokeWeather.Last == null)
            {
                return;
            }

            weather.curWeather = smokeWeather.Current;
            weather.lastWeather = smokeWeather.Last;
            weather.curWeatherAge = smokeWeather.Age;
            smokeWeather = default;
        }

        /// <summary>
        /// The map's weather as the session sees it, for a check's detail:
        /// "held X; current from last, age n".
        /// </summary>
        /// <param name="map">The map.</param>
        /// <returns>The text.</returns>
        private string HeldWeatherNote(Map map) =>
            $"held {showcaseSession.HeldWeather?.defName ?? "none"}; "
                + WeatherNote(map?.weatherManager);

        /// <summary>
        /// The hold replaced the fog at once as the showcase began, and
        /// on the next tick undoes another weather and sets back an age
        /// past the decider's turn.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="fog">The weather set before the showcase.</param>
        /// <param name="atStart">The weather noted just after it began.</param>
        private void CheckWeatherHeldAtStart(Map map, WeatherDef fog, string atStart)
        {
            WeatherManager weather = map.weatherManager;
            Check("a showcase holds its def's weather from its start: the "
                    + "weather it found is replaced at once",
                fog != null && atStart == "held Clear; Clear from Clear, age 0",
                $"fog {fog?.defName ?? "missing"}; at the start: {atStart}");

            WeatherDef rain = DefDatabase<WeatherDef>.GetNamedSilentFail("Rain");
            weather.TransitionTo(rain ?? fog);
            weather.curWeatherAge = 5000;
            int restarts = showcaseSession.WeatherRestarts;
            Tick(1);
            Check("the held weather undoes another weather on the next tick, "
                    + "with its age under the weather decider's turn",
                ShowsHeld(WeatherDefOf.Clear, weather)
                    && weather.lastWeather == WeatherDefOf.Clear
                    && weather.curWeatherAge <= WeatherHold.MaxHeldAge
                    && showcaseSession.WeatherRestarts == restarts + 1,
                $"{HeldWeatherNote(map)}; restarts {restarts} to "
                    + showcaseSession.WeatherRestarts);
        }

        /// <summary>
        /// Lends the weather to the running scene and starts rain: the
        /// hold stands aside, so the rain stays for two ticks.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="scene">The running scene.</param>
        /// <returns>Whether the rain stayed while the weather was lent.</returns>
        private bool LendWeatherToScene(Map map, ShowcaseScene scene)
        {
            WeatherManager weather = map.weatherManager;
            WeatherDef rain = DefDatabase<WeatherDef>.GetNamedSilentFail("Rain");
            if (scene == null || rain == null)
            {
                return false;
            }

            showcaseSession.YieldWeather(scene);
            weather.TransitionTo(rain);
            Tick(2);
            return showcaseSession.WeatherLent && weather.curWeather == rain;
        }

        /// <summary>
        /// The weather lent to a scene stayed the scene's while it ran,
        /// and came back to the hold by itself once the scene stopped
        /// running (here, skipped without giving it back).
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="stayed">Whether the scene's rain stayed while lent.</param>
        private void CheckLentWeatherReturns(Map map, bool stayed)
        {
            Tick(1);
            Check("a scene can borrow the held weather, and the hold takes it "
                    + "back once that scene is no longer running",
                stayed && !showcaseSession.WeatherLent
                    && ShowsHeld(WeatherDefOf.Clear, map.weatherManager)
                    && map.weatherManager.lastWeather == WeatherDefOf.Clear,
                $"rain stayed while lent {stayed}; lent now "
                    + $"{showcaseSession.WeatherLent}; {HeldWeatherNote(map)}");
        }

        /// <summary>
        /// The session gave the weather from before the showcase (fog)
        /// back as the showcase ended, and holds none.
        /// </summary>
        /// <param name="map">The map.</param>
        /// <param name="fog">The weather from before.</param>
        /// <param name="how">How the showcase ended, for the check's name.</param>
        private void CheckWeatherGivenBack(Map map, WeatherDef fog, string how)
        {
            WeatherManager weather = map?.weatherManager;
            Check($"the held weather is given back when a showcase {how}",
                fog != null && weather != null && showcaseSession.HeldWeather == null
                    && weather.curWeather == fog && weather.lastWeather == fog,
                $"fog {fog?.defName ?? "missing"}; {HeldWeatherNote(map)}");
        }
    }
}
