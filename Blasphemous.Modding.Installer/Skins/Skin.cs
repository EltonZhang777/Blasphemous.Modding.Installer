﻿using Basalt.Framework.Logging;
using Blasphemous.Modding.Installer.Extensions;
using Newtonsoft.Json;

namespace Blasphemous.Modding.Installer.Skins;

internal class Skin
{
    private readonly SkinUI _ui;
    private readonly SectionType _skinType;

    private bool _downloading;

    public Skin(SkinData data, SectionType skinType)
    {
        Data = data;
        _skinType = skinType;
        _ui = new SkinUI(this);
        SetUIVisibility(false);
        SetUIPosition(-1);
        UpdateUI();
    }

    public SkinData Data { get; set; }

    private InstallerPage SkinPage => Core.Blas1SkinPage;

    public bool Installed => File.Exists(PathToSkinFolder + "/info.txt");

    public Version LocalVersion
    {
        get
        {
            string infoPath = PathToSkinFolder + "/info.txt";
            if (Installed)
            {
                SkinData data = JsonConvert.DeserializeObject<SkinData>(File.ReadAllText(infoPath));
                return GithubHandler.CleanSemanticVersion(data.version);
            }
            else
            {
                return null;
            }
        }
    }

    public bool UpdateAvailable
    {
        get
        {
            if (!Installed)
                return false;

            return GithubHandler.CleanSemanticVersion(Data.version).CompareTo(LocalVersion) > 0;
        }
    }

    // Paths

    private string RootFolder => Core.SettingsHandler.Properties.GetRootPath(_skinType);
    public string PathToSkinFolder => $"{RootFolder}/Modding/skins/{Data.id}";

    private string SubFolder => "blasphemous1";
    public string InfoURL => $"https://raw.githubusercontent.com/BrandenEK/Blasphemous-Custom-Skins/main/{SubFolder}/{Data.id}/info.txt";
    public string TextureURL => $"https://raw.githubusercontent.com/BrandenEK/Blasphemous-Custom-Skins/main/{SubFolder}/{Data.id}/texture.png";
    public string PreviewURL => $"https://raw.githubusercontent.com/BrandenEK/Blasphemous-Custom-Skins/main/{SubFolder}/{Data.id}/preview.png";

    public bool ExistsInCache(string fileName, out string cachePath)
    {
        cachePath = $"{Core.CacheFolder}/blas1skins/{Data.id}/{Data.version}/{fileName}";
        Directory.CreateDirectory(Path.GetDirectoryName(cachePath));

        return File.Exists(cachePath) && new FileInfo(cachePath).Length > 0;
    }

    // Main methods

    public async void Install(bool refreshList)
    {
        string installPath = PathToSkinFolder;
        Directory.CreateDirectory(installPath);

        // Check for files in the cache
        bool infoExists = ExistsInCache("info.txt", out string infoCache);
        bool textureExists = ExistsInCache("texture.png", out string textureCache);

        // If they were missing, download them from web to cache
        if (!infoExists || !textureExists)
        {
            await DownloadSkin(infoCache, textureCache);
        }

        // Copy files from cache to game folder
        File.Copy(infoCache, installPath + "/info.txt");
        File.Copy(textureCache, installPath + "/texture.png");

        UpdateUI();
        if (refreshList)
            SkinPage.Lister.RefreshList();
    }

    private async Task DownloadSkin(string infoCache, string textureCache)
    {
        Logger.Warn($"Downloading skin texture ({Data.name}) from web");
        using var client = new HttpClient();

        _downloading = true;
        _ui.ShowDownloadingStatus();

        await client.DownloadFileAsync(new Uri(InfoURL), infoCache);
        await client.DownloadFileAsync(new Uri(TextureURL), textureCache);

        _downloading = false;
    }

    public void Uninstall(bool refreshList)
    {
        if (Directory.Exists(PathToSkinFolder))
            Directory.Delete(PathToSkinFolder, true);

        UpdateUI();
        if (refreshList)
            SkinPage.Lister.RefreshList();
    }

    // Click methods

    public void ClickedInstall(object sender, EventArgs e)
    {
        if (_downloading) return;

        if (Installed)
        {
            if (MessageBox.Show("Are you sure you want to uninstall this skin?", Data.name, MessageBoxButtons.OKCancel) == DialogResult.OK)
                Uninstall(true);
        }
        else
        {
            Install(true);
        }
    }

    public void ClickedUpdate(object sender, EventArgs e)
    {
        Uninstall(false);
        Install(true);
    }

    // UI methods

    public void UpdateUI()
    {
        _ui.UpdateUI(Data.name, Data.author, Installed, UpdateAvailable);
    }

    public void SetUIPosition(int skinIdx)
    {
        _ui.SetPosition(skinIdx);
    }

    public void SetUIVisibility(bool visible)
    {
        _ui.SetVisibility(visible);
    }

    public void OnStartHover() => SkinPage.Previewer.PreviewSkin(this);

    public void OnEndHover() => SkinPage.Previewer.Clear();
}
