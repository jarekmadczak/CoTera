﻿using CoTera.Views;
using Newtonsoft.Json.Linq;
using Octokit;

namespace CoTera.Systems
{
    //TD: Optimize git connection so app dont use token
    //Optimize checking if there is internet connection, maybe call alert if there is no
    //Move all string to const
    internal static class DataLoaderSystem
    {
        //$("meta[name=octolytics-dimension-repository_id]").getAttribute('content')
        internal const long REPOID = 793261339;

        internal static string? SavedSelectedYear;

        internal static string? SavedSelectedLab;

        internal static GitHubClient? GitClient;

        internal static string LoadedJsonFile = "";

        internal static List<string>? LoadedWeeksTypeA;


        internal static void InitializeGitConnection()
        {
            if (GitClient != null)
                return;

            GitClient = new GitHubClient(new ProductHeaderValue("GettingDataFromGitDB"));
        }

        static void SaveDataToCache()
        {
            string MainDataFile = Path.Combine(FileSystem.CacheDirectory + "/CoTera_SavedData.txt");

            string savedData = "{Y}=" + SavedSelectedYear + "\n";
            savedData += "{L}=" + SavedSelectedLab + "\n";
            savedData += "{JSON}\n" + LoadedJsonFile;

            File.WriteAllText(MainDataFile, savedData);
        }

        static void GenerateAppDataBasedOnLoadedJsonFile()
        {
            var parsedJson = JObject.Parse(LoadedJsonFile);

            for (int i = 0; i < 5; i++)
            {
                var rawClassesViews = parsedJson.Values().ToArray()[i];

                List<ClassView> classes = new List<ClassView>();
                foreach (var rawClass in rawClassesViews)
                    classes.Add(new ClassView(rawClass));

                MainPage.Instance!.LoadedDays[i] = new DayView((DayOfWeek)i + 1, classes.ToArray());
            }

            //refresh showed classes after loading them
            MainPage.Instance!.ShowClassesForCurrentDay();
        }

        internal static async void LoadSavedOrDefaultData()
        {
            string MainDataFile = Path.Combine(FileSystem.CacheDirectory + "/CoTera_SavedData.txt");
            string WeeksDataFile = Path.Combine(FileSystem.CacheDirectory + "/CoTera_WeeksTypeA.txt");

            //load saved class plan
            if (File.Exists(MainDataFile))
            {
                string load = File.ReadAllText(MainDataFile);

                SavedSelectedYear = File.ReadAllLines(MainDataFile)[0].Substring(4);
                SavedSelectedLab = File.ReadAllLines(MainDataFile)[1].Substring(4);
                LoadedJsonFile = load.Substring(load.IndexOf("{JSON}") + 6);
            }
            else
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet && Connectivity.Current.NetworkAccess != NetworkAccess.ConstrainedInternet)
                    return;

                await FetchAndSetJsonFileContentFromLink("PlanyZajec/ExampleFolder/ExLab1.json");

                SavedSelectedYear = "ExampleFolder";
                SavedSelectedLab = "ExLab1";

                SaveDataToCache();
            }

            //load saved weeks type a
            if (File.Exists(WeeksDataFile))
                LoadedWeeksTypeA = File.ReadAllLines(WeeksDataFile).ToList();
            else
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet && Connectivity.Current.NetworkAccess != NetworkAccess.ConstrainedInternet)
                    return;

                await FetchWeeksTypeA();

                string savedData = "";
                for(int i=0;i< LoadedWeeksTypeA!.Count; i++)
                    savedData += LoadedWeeksTypeA[i] +"\n";

                File.WriteAllText(WeeksDataFile,savedData);
            }

            MainPage.Instance!.ChosenDate = DateTime.Now;

            GenerateAppDataBasedOnLoadedJsonFile();
        }

        internal static async void GetAllYears() => await FetchAllYears();

        internal static async void GetAllLabsForCurrentYear() => await FetchLabsForCurrentYear();

        internal static async void GetSelectedOptionsContent() => await FetchSelectedOptionsData();

        static async Task FetchAllYears()
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet && Connectivity.Current.NetworkAccess != NetworkAccess.ConstrainedInternet)
                return;

            var loadedData = new List<string>();

            var contents = await GitClient!.Repository.Content.GetAllContents(REPOID, "PlanyZajec");

            foreach (var year in contents)
                if (year.Type == ContentType.Dir)
                    loadedData.Add(year.Name);

            OptionsPage.Instance!.LoadedYears = loadedData;

            //check if user selected any year previously, if so load that data
            OptionsPage.Instance.SelectedYearIndex = loadedData.IndexOf(SavedSelectedYear!) != -1 ? loadedData.IndexOf(SavedSelectedYear!) : 0;
        }

        static async Task FetchLabsForCurrentYear()
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet && Connectivity.Current.NetworkAccess != NetworkAccess.ConstrainedInternet)
                return;

            if (OptionsPage.Instance!.SelectedYearIndex == -1 || OptionsPage.Instance.LoadedYears[0] == "-")
                return;

            string selectedLabPath = "PlanyZajec/" + OptionsPage.Instance.LoadedYears[OptionsPage.Instance.SelectedYearIndex];

            var loadedData = new List<string>();

            var contents = await GitClient!.Repository.Content.GetAllContents(REPOID, selectedLabPath);

            foreach (var lab in contents)
                if (lab.Type == ContentType.File)
                    loadedData.Add(lab.Name.Substring(0, lab.Name.IndexOf(".json")));

            OptionsPage.Instance.LoadedLabs = loadedData;
            OptionsPage.Instance.SelectedLabIndex = loadedData.IndexOf(SavedSelectedLab!) != -1 ? loadedData.IndexOf(SavedSelectedLab!) : 0;
        }
        static async Task FetchSelectedOptionsData()
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet && Connectivity.Current.NetworkAccess != NetworkAccess.ConstrainedInternet)
                return;

            await FetchAndSetJsonFileContentFromLink("PlanyZajec/" + SavedSelectedYear + "/" + SavedSelectedLab + ".json");

            SaveDataToCache();
            GenerateAppDataBasedOnLoadedJsonFile();
        }

        static async Task FetchWeeksTypeA()
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet && Connectivity.Current.NetworkAccess != NetworkAccess.ConstrainedInternet)
                return;

            InitializeGitConnection();
            var request = await GitClient!.Repository.Content.GetAllContents(REPOID, "Tygodnie/Tygodnie_A.json");

            LoadedWeeksTypeA = JArray.Parse(request[0].Content).Select(e => e.ToString()).ToList();
        }

        static async Task FetchAndSetJsonFileContentFromLink(string link)
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet && Connectivity.Current.NetworkAccess != NetworkAccess.ConstrainedInternet)
                return;

            InitializeGitConnection();
            var request = await GitClient!.Repository.Content.GetAllContents(REPOID, link);

            LoadedJsonFile = request[0].Content;
        }
    }
}