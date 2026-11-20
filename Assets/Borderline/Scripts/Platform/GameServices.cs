using OGSoftware.Platform;
using UnityEngine;

namespace OGSoftware.Platform
{
    /// <summary>This game's Play Games App ID, read by OGPlatformBuild when the Android project is generated.</summary>
    public static class OGPlatformProject
    {
        /// <summary>Play Console → Play Games Services → Configuration → "Project ID" (a long number).</summary>
        public const string AndroidAppId = "TODO_PLAY_GAMES_APP_ID";
    }
}

namespace Borderline
{
    /// <summary>
    /// The player's lifetime record: matches, wins, hexes claimed and captured, turrets built, fastest win. Saved on
    /// the device (persistentDataPath/borderline_profile.json) and backed up to Game Center / Play Games.
    /// </summary>
    [System.Serializable]
    public class PlayerProfile
    {
        public long revision;          // +1 on every save; picks between this device's and the cloud's copy
        public int matches, wins, losses;
        public int hexesClaimed, hexesCaptured, turretsBuilt;
        public int fastestWinSeconds;  // 0 = no win yet
        public float secondsPlayed;

        const string FileName = "borderline_profile.json";
        static string PathOnDisk => System.IO.Path.Combine(Application.persistentDataPath, FileName);

        public static PlayerProfile Load()
        {
            try
            {
                if (System.IO.File.Exists(PathOnDisk))
                {
                    var p = JsonUtility.FromJson<PlayerProfile>(System.IO.File.ReadAllText(PathOnDisk));
                    if (p != null) return p;
                }
            }
            catch (System.Exception e) { Debug.LogWarning("[Borderline] couldn't read the profile: " + e.Message); }
            return new PlayerProfile();
        }

        /// <summary>Writes the profile as it is.</summary>
        public void Write()
        {
            try
            {
                string tmp = PathOnDisk + ".tmp";
                System.IO.File.WriteAllText(tmp, JsonUtility.ToJson(this));
                if (System.IO.File.Exists(PathOnDisk)) System.IO.File.Delete(PathOnDisk);
                System.IO.File.Move(tmp, PathOnDisk);
            }
            catch (System.Exception e) { Debug.LogWarning("[Borderline] couldn't save the profile: " + e.Message); }
        }
    }

    /// <summary>
    /// Borderline on Game Center / Google Play Games: achievements, leaderboards and the cloud copy of the player's
    /// profile. Shared code: Assets/OGPlatform (same in every OG Software game). Store setup: GAME_SERVICES.md.
    /// GameManager reports what happens in a match; everything is worked out from the profile.
    /// </summary>
    public static class GameServices
    {
        const string Ios = "com.ogsoftware.borderline.";

        public const string FirstWin = "first_win", Wins10 = "wins_10", Claims100 = "claims_100",
            Captures50 = "captures_50", FirstTurret = "first_turret", Turrets25 = "turrets_25", Blitz = "blitz";

        public const string LbWins = "wins", LbFastest = "fastest_win";

        const int BlitzSeconds = 300;

        /// <summary>Play Console IDs (they look like CgkI...EAIQAQ). Leave TODO until they exist; nothing is sent for those.</summary>
        static string Android(string key)
        {
            switch (key)
            {
                case FirstWin: return "TODO";
                case Wins10: return "TODO";
                case Claims100: return "TODO";
                case Captures50: return "TODO";
                case FirstTurret: return "TODO";
                case Turrets25: return "TODO";
                case Blitz: return "TODO";
                case LbWins: return "TODO";
                case LbFastest: return "TODO";
            }
            return "TODO";
        }

        public static PlayerProfile Profile { get; private set; }
        static bool initialized;

        /// <summary>Safe to call on every scene load (Restart reloads the scene).</summary>
        public static void Init()
        {
            if (initialized) return;
            initialized = true;
            Profile = PlayerProfile.Load();

            void A(string key, int steps = 1) => OGPlatform.DefineAchievement(key, Ios + key, Android(key), steps);
            A(FirstWin); A(Wins10, 10); A(Claims100, 100); A(Captures50, 50); A(FirstTurret); A(Turrets25, 25); A(Blitz);
            OGPlatform.DefineLeaderboard(LbWins, Ios + LbWins, Android(LbWins));
            OGPlatform.DefineLeaderboard(LbFastest, Ios + LbFastest, Android(LbFastest), lowerIsBetter: true);

            OGPlatform.SignInChanged += () => { if (OGPlatform.IsSignedIn) Evaluate(); };
            OGCloudSave.Register(new CloudData());
            OGPlatform.Init();
            Evaluate();
        }

        // ---- reported by GameManager (saved at the end of each match and when the app is backgrounded)
        public static void HexClaimed() { if (Profile != null) Profile.hexesClaimed++; }
        public static void HexCaptured() { if (Profile != null) Profile.hexesCaptured++; }
        public static void TurretBuilt() { if (Profile != null) { Profile.turretsBuilt++; Save(); } }
        public static void Played(float seconds) { if (Profile != null) Profile.secondsPlayed += seconds; }

        public static void MatchEnded(bool won, float matchSeconds)
        {
            if (Profile == null) return;
            Profile.matches++;
            if (won)
            {
                Profile.wins++;
                int s = Mathf.Max(1, Mathf.RoundToInt(matchSeconds));
                if (Profile.fastestWinSeconds == 0 || s < Profile.fastestWinSeconds) Profile.fastestWinSeconds = s;
            }
            else Profile.losses++;
            Save();
        }

        public static void Save()
        {
            if (Profile == null) return;
            Profile.revision++;
            Profile.Write();
            Evaluate();
            OGCloudSave.MarkDirty();
        }

        static void Evaluate()
        {
            var p = Profile;
            if (p == null) return;
            if (p.wins > 0) OGPlatform.Unlock(FirstWin);
            OGPlatform.SetProgress(Wins10, p.wins);
            OGPlatform.SetProgress(Claims100, p.hexesClaimed);
            OGPlatform.SetProgress(Captures50, p.hexesCaptured);
            if (p.turretsBuilt > 0) OGPlatform.Unlock(FirstTurret);
            OGPlatform.SetProgress(Turrets25, p.turretsBuilt);
            if (p.fastestWinSeconds > 0 && p.fastestWinSeconds <= BlitzSeconds) OGPlatform.Unlock(Blitz);
            if (p.wins > 0) OGPlatform.SubmitScore(LbWins, p.wins);
            if (p.fastestWinSeconds > 0) OGPlatform.SubmitScore(LbFastest, p.fastestWinSeconds);
        }

        class CloudData : IOGCloudData
        {
            public string Export() => JsonUtility.ToJson(Profile);

            public long Progress(string exported)
            {
                var p = JsonUtility.FromJson<PlayerProfile>(exported);
                return p != null ? p.revision : 0;
            }

            public bool LocalIsEmpty => Profile == null || Profile.revision == 0;

            // the profile is only a record, so it can be swapped at any time, even mid-match
            public void Import(string exported)
            {
                var p = JsonUtility.FromJson<PlayerProfile>(exported);
                if (p == null) throw new System.Exception("not a Borderline profile");
                Profile = p;
                p.Write();
                Evaluate();
            }
        }
    }
}
