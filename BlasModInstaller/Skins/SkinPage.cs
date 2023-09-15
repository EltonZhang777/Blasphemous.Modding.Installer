﻿using BlasModInstaller.Grouping;
using BlasModInstaller.Sorting;
using BlasModInstaller.UIHolding;
using BlasModInstaller.Validation;
using Ionic.Zip;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BlasModInstaller.Skins
{
    internal class SkinPage : BasePage
    {
        private readonly List<Skin> _skins = new List<Skin>();
        private readonly SkinGrouper _grouper;
        private readonly GenericUIHolder<Skin> _uiHolder;
        private readonly SkinSorter _sorter;

        private bool _loaded = false;

        public SkinPage(string title, Bitmap image, Panel panel, string localDataPath, string globalDataPath, IValidator validator)
            : base(title, image, localDataPath, globalDataPath, validator)
        {
            _grouper = new SkinGrouper(title, _skins);
            _uiHolder = new GenericUIHolder<Skin>(panel, _skins);
            _sorter = new SkinSorter(_uiHolder, _skins);
        }

        public override IGrouper Grouper => _grouper;
        public override IUIHolder UIHolder => _uiHolder;
        public override ISorter Sorter => _sorter;

        // Skin list

        private bool SkinExists(string id, out Skin existingSkin)
        {
            foreach (Skin skin in _skins)
            {
                if (skin.id == id)
                {
                    existingSkin = skin;
                    return true;
                }
            }
            existingSkin = null;
            return false;
        }

        // Data

        public override void LoadData()
        {
            _uiHolder.AdjustPageWidth();
            if (_loaded)
                return;

            LoadLocalSkins();
            LoadGlobalSkins();
            _loaded = true;
        }

        private void LoadLocalSkins()
        {
            if (File.Exists(_localDataPath))
            {
                string json = File.ReadAllText(_localDataPath);
                Skin[] localSkins = JsonConvert.DeserializeObject<Skin[]>(json);
                _skins.AddRange(localSkins);
            }

            for (int i = 0; i < _skins.Count; i++)
                _skins[i].CreateUI(_uiHolder.SectionPanel, i);

            Core.UIHandler.Log($"Loaded {_skins.Count} local skins");
            _uiHolder.SetBackgroundColor();
            _sorter.Sort();
        }

        private async Task LoadGlobalSkins()
        {
            using (HttpClient client = new HttpClient())
            {
                IReadOnlyList<Octokit.RepositoryContent> contents = await Core.GithubHandler.GetRepositoryContents("BrandenEK", "Blasphemous-Custom-Skins");
                foreach (var item in contents)
                {
                    string json = await client.GetStringAsync($"https://raw.githubusercontent.com/BrandenEK/Blasphemous-Custom-Skins/main/{item.Name}/info.txt");
                    Skin globalSkin = JsonConvert.DeserializeObject<Skin>(json);

                    if (SkinExists(globalSkin.id, out Skin localSkin))
                    {
                        localSkin.UpdateLocalData(globalSkin);
                        localSkin.UpdateUI();
                    }
                    else
                    {
                        _skins.Add(globalSkin);
                        globalSkin.CreateUI(_uiHolder.SectionPanel, _skins.Count - 1);
                    }
                }

                Core.UIHandler.Log($"Loaded {contents.Count} global skins");
            }

            SaveLocalData();
            _uiHolder.SetBackgroundColor();
            _sorter.Sort();
        }

        private void SaveLocalData()
        {
            File.WriteAllText(_localDataPath, JsonConvert.SerializeObject(_skins));
        }

        public override async Task InstallTools()
        {
            using (WebClient client = new WebClient())
            {
                string downloadPath = $"{UIHandler.DownloadsPath}{"Blas1_Tools"}.zip";
                string installPath = Core.SettingsHandler.Config.Blas1RootFolder;

                // Get this from somewhere else later
                string temp = "https://github.com/BrandenEK/Blasphemous.ModdingTools/raw/main/modding-tools.zip";
                await client.DownloadFileTaskAsync(new Uri(temp), downloadPath);

                using (ZipFile zipFile = ZipFile.Read(downloadPath))
                {
                    foreach (ZipEntry file in zipFile)
                        file.Extract(installPath, ExtractExistingFileAction.OverwriteSilently);
                }

                File.Delete(downloadPath);
            }
        }

        public override SortType CurrentSortType
        {
            get => Core.SettingsHandler.Config.Blas1SkinSort;
            set => Core.SettingsHandler.Config.Blas1SkinSort = value;
        }
    }
}
