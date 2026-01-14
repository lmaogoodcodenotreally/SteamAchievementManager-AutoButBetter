using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.XPath;
using APITypes = SAM.API.Types;
using Microsoft.Win32;
using System.Text.RegularExpressions;


namespace SAM.Picker
{
    internal partial class GamePicker : Form
    {
        // Method to get the Steam installation path
        private string GetSteamInstallPath()
        {
            return (string)Registry.GetValue(@"HKEY_CURRENT_USER\SOFTWARE\Valve\Steam", "SteamPath", null);
        }

        // Method to get the current Steam ID
        private string GetCurrentSteamId()
        {
            string installPath = GetSteamInstallPath();
            if (installPath == null)
            {
                Console.WriteLine("Could not find the Steam installation path.");
                return null;
            }

            string filePath = Path.Combine(installPath, "config", "loginusers.vdf");
            Console.WriteLine($"Attempting to read file: {filePath}");
            if (!File.Exists(filePath))
            {
                return null;
            }

            string contents;
            try
            {
                contents = File.ReadAllText(filePath);
            }
            catch
            {
                return null;
            }

            Console.WriteLine($"File contents:\n{contents}");
            Regex regex = new Regex(@"""(\d{17})""\s*\{[^}]*""MostRecent""\s*""1""");
            Match match = regex.Match(contents);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            return null;
        }

        // Method to update the window title
        private void UpdateWindowTitle()
        {
            string steamId = GetCurrentSteamId();
            if (steamId != null)
            {
                this.Text = $"  SAM-ABB   ·   Connected as {steamId}   ·   1.0.3-beta (ik UI is trash)";
            }
            else
            {
                this.Text = "  SAM-ABB   ·   Loading...   ·   1.0.3-beta (ik UI is trash)";
            }
        }

        private void CreateSaveDirectory(string steamId)
        {
            string directoryPath = Path.Combine(".", "data", "saves", steamId);
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
                Console.WriteLine($"Directory created: {directoryPath}");
            }
            else
            {
                Console.WriteLine($"Directory already exists: {directoryPath}");
            }
        }

        private readonly API.Client _SteamClient;
        private readonly List<GameInfo> _Games;
        private readonly List<GameInfo> _FilteredGames;
        private int _SelectedGameIndex;
        private HashSet<string> _IgnoredGameIds; // Keep this declaration

        public List<GameInfo> Games
        {
            get { return _Games; }
        }

        private readonly List<string> _LogosAttempted;
        private readonly ConcurrentQueue<GameInfo> _LogoQueue;

        // ReSharper disable PrivateFieldCanBeConvertedToLocalVariable
        private readonly API.Callbacks.AppDataChanged _AppDataChangedCallback;
        // ReSharper restore PrivateFieldCanBeConvertedToLocalVariable
        
        // Remove the duplicate declaration of _IgnoredGameIds
        public GamePicker(API.Client client)
        {
            this._Games = new List<GameInfo>();
            this._FilteredGames = new List<GameInfo>();
            this._SelectedGameIndex = -1;
            this._LogosAttempted = new List<string>();
            this._LogoQueue = new ConcurrentQueue<GameInfo>();
            this._IgnoredGameIds = new HashSet<string>(); // Initialize here
            this.InitializeComponent();
            string steamId = GetCurrentSteamId();
            if (steamId != null)
            {
                CreateSaveDirectory(steamId);
                string filePath = Path.Combine(".", "data", "saves", steamId, "save.unlocks");
                UpdateWindowTitle();
                if (File.Exists(filePath))
                {
                    foreach (var line in File.ReadLines(filePath))
                    {
                        _IgnoredGameIds.Add(line.Trim());
                    }
                }
            }
            var blank = new Bitmap(this._LogoImageList.ImageSize.Width, this._LogoImageList.ImageSize.Height);
            using (var g = Graphics.FromImage(blank))
            {
                g.Clear(Color.DimGray);
            }

            this._LogoImageList.Images.Add ("Blank", blank);

            this._SteamClient = client;

            this._AppDataChangedCallback = client.CreateAndRegisterCallback<API.Callbacks.AppDataChanged>();
            this._AppDataChangedCallback.OnRun += this.OnAppDataChanged;

            this.AddGames();
        }

        private void OnAppDataChanged(APITypes.AppDataChanged param)
        {
            if (param.Result == true)
            {
                foreach (GameInfo info in this._Games)
                {
                    if (info.Id == param.Id)
                    {
                        info.Name = this._SteamClient.SteamApps001.GetAppData(info.Id, "name");
                        this.AddGameToLogoQueue(info);
                        break;
                    }
                }
            }
        }

        private void DoDownloadList(object sender, DoWorkEventArgs e)
        {
            var pairs = new List<KeyValuePair<uint, string>>();
            byte[] bytes;
            using (var downloader = new WebClient())
            {
                bytes = downloader.DownloadData(new Uri(string.Format("http://gib.me/sam/games.xml")));
            }
            using (var stream = new MemoryStream(bytes, false))
            {
                var document = new XPathDocument(stream);
                var navigator = document.CreateNavigator();
                var nodes = navigator.Select("/games/game");
                while (nodes.MoveNext())
                {
                    string type = nodes.Current.GetAttribute("type", "");
                    if (type == string.Empty)
                    {
                        type = "normal";
                    }
                    pairs.Add(new KeyValuePair<uint, string>((uint)nodes.Current.ValueAsLong, type));
                }
            }
            e.Result = pairs;
        }

        private void OnDownloadList(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error == null && e.Cancelled == false)
            {
                var pairs = (List<KeyValuePair<uint, string>>)e.Result;
                foreach (var kv in pairs)
                {
                    this.AddGame(kv.Key, kv.Value);
                }
            }
            else
            {
                this.AddDefaultGames();
                //MessageBox.Show(e.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            this.RefreshGames();
            this._RefreshGamesButton.Enabled = true;
            this.unlockAllGames.Enabled = true;
            this.DownloadNextLogo();
        }
        private void RefreshGames()
        {
            // Ensure _IgnoredGameIds is initialized before use
            if (_IgnoredGameIds == null)
            {
                _IgnoredGameIds = new HashSet<string>(); // Initialize if null
            }

            this._FilteredGames.Clear();

            // Check if _Games is initialized
            if (_Games == null)
            {
                Console.WriteLine("Error: _Games is not initialized.");
                return; // Exit if _Games is null
            }

            foreach (var info in this._Games.OrderBy(gi => gi.Name))
            {
                // Ensure info is not null
                if (info == null)
                {
                    Console.WriteLine("Warning: info is null. Skipping.");
                    continue; // Skip this iteration if info is null
                }

                // Check if the game ID is in the ignored list
                if (_IgnoredGameIds.Contains(info.Id.ToString()))
                {
                    continue;
                }

                // Filter games based on type and user selections
                if (info.Type == "normal" && !_FilterGamesMenuItem.Checked)
                {
                    continue;
                }
                if (info.Type == "demo" && !_FilterDemosMenuItem.Checked)
                {
                    continue;
                }
                if (info.Type == "mod" && !_FilterModsMenuItem.Checked)
                {
                    continue;
                }
                if (info.Type == "junk" && !_FilterJunkMenuItem.Checked)
                {
                    continue;
                }

                // Add the valid game to the filtered list
                this._FilteredGames.Add(info);
            }

            // Update the GameListView with the filtered games
            this._GameListView.BeginUpdate();
            this._GameListView.VirtualListSize = this._FilteredGames.Count;
            if (this._FilteredGames.Count > 0)
            {
                this._GameListView.RedrawItems(0, this._FilteredGames.Count - 1, true);
            }
            this._GameListView.EndUpdate();

            // Update the status label with the count of displayed and total games
            this._PickerStatusLabel.Text = string.Format(
                "Displaying {0} games. Total {1} games.",
                this._GameListView.Items.Count,
                this._Games.Count);
        }

        private void OnGameListViewRetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            var info = this._FilteredGames[e.ItemIndex];
            e.Item = new ListViewItem()
            {
                Text = info.Name,
                ImageIndex = info.ImageIndex,
            };
        }

        private void DoDownloadLogo(object sender, DoWorkEventArgs e)
        {
            var info = (GameInfo)e.Argument;
            var logoPath = string.Format(
                "http://media.steamcommunity.com/steamcommunity/public/images/apps/{0}/{1}.jpg",
                info.Id,
                info.Logo);
            using (var downloader = new WebClient())
            {
                var data = downloader.DownloadData(new Uri(logoPath));

                try
                {
                    using (var stream = new MemoryStream(data, false))
                    {
                        var bitmap = new Bitmap(stream);
                        e.Result = new LogoInfo(info.Id, bitmap);
                    }
                }
                catch (Exception)
                {
                    e.Result = new LogoInfo(info.Id, null);
                }
            }
        }

        private void OnDownloadLogo(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null || e.Cancelled == true)
            {
                return;
            }

            var logoInfo = (LogoInfo)e.Result;
            var gameInfo = this._Games.FirstOrDefault(gi => gi.Id == logoInfo.Id);
            if (gameInfo != null && logoInfo.Bitmap != null)
            {
                this._GameListView.BeginUpdate();
                var imageIndex = this._LogoImageList.Images.Count;
                this._LogoImageList.Images.Add(gameInfo.Logo, logoInfo.Bitmap);
                gameInfo.ImageIndex = imageIndex;
                this._GameListView.EndUpdate();
            }

            this.DownloadNextLogo();
        }

        private void DownloadNextLogo()
        {
            if (this._LogoWorker.IsBusy)
            {
                return;
            }

            GameInfo info;
            if (this._LogoQueue.TryDequeue(out info) == false)
            {
                this._DownloadStatusLabel.Visible = false;
                return;
            }

            this._DownloadStatusLabel.Text = string.Format(
                "Downloading {0} game icons...",
                this._LogoQueue.Count);
            this._DownloadStatusLabel.Visible = true;

            this._LogoWorker.RunWorkerAsync(info);
        }

        private void AddGameToLogoQueue(GameInfo info)
        {
            string logo = this._SteamClient.SteamApps001.GetAppData(info.Id, "logo");

            if (logo == null)
            {
                return;
            }

            info.Logo = logo;

            int imageIndex = this._LogoImageList.Images.IndexOfKey(logo);
            if (imageIndex >= 0)
            {
                info.ImageIndex = imageIndex;
            }
            else if (this._LogosAttempted.Contains(logo) == false)
            {
                this._LogosAttempted.Add(logo);
                this._LogoQueue.Enqueue(info);
                this.DownloadNextLogo();
            }
        }

        private bool OwnsGame(uint id)
        {
            return this._SteamClient.SteamApps003.IsSubscribedApp(id);
        }

        private void AddGame(uint id, string type)
        {
            if (this._Games.Any(i => i.Id == id) == true)
            {
                return;
            }

            if (this.OwnsGame(id) == false)
            {
                return;
            }

            var info = new GameInfo(id, type);
            info.Name = this._SteamClient.SteamApps001.GetAppData(info.Id, "name");

            this._Games.Add(info);
            this.AddGameToLogoQueue(info);
        }

        private void AddGames()
        {
            this._Games.Clear();
            this._RefreshGamesButton.Enabled = false;
            this._ListWorker.RunWorkerAsync();
        }

        private void AddDefaultGames()
        {
            this.AddGame(480, "normal"); // Spacewar
        }

        private void OnTimer(object sender, EventArgs e)
        {
            this._CallbackTimer.Enabled = false;
            this._SteamClient.RunCallbacks(false);
            this._CallbackTimer.Enabled = true;
        }

        private void OnSelectGame(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            if (e.IsSelected == true && e.ItemIndex != this._SelectedGameIndex)
            {
                this._SelectedGameIndex = e.ItemIndex;
            }
            else if (e.IsSelected == true && e.ItemIndex == this._SelectedGameIndex)
            {
                this._SelectedGameIndex = -1;
            }
        }

        private void OnActivateGame(object sender, EventArgs e)
        {
            if (this._SelectedGameIndex < 0)
            {
                return;
            }

            var index = this._SelectedGameIndex;
            if (index < 0 || index >= this._FilteredGames.Count)
            {
                return;
            }

            var info = this._FilteredGames[index];
            if (info == null)
            {
                return;
            }

            string currentSteamId = GetCurrentSteamId();
            if (currentSteamId != null)
            {
                Console.WriteLine($"Current SteamID64: {currentSteamId}");
                CreateSaveDirectory(currentSteamId);
                string filePath = Path.Combine(".", "data", "saves", currentSteamId, "save.unlocks");
                try
                {
                // Check if the file already exists
                if (File.Exists(filePath))
                {
                    // Read existing game IDs
                    var existingIds = new HashSet<string>(File.ReadAllLines(filePath).Select(id => id.Trim()));

                    // Append the new game ID if not already present
                    if (!existingIds.Contains(info.Id.ToString()))
                    {
                        File.AppendAllText(filePath, info.Id.ToString() + "\n", System.Text.Encoding.UTF8);
                        Console.WriteLine($"Game ID appended to: {filePath}");
                    }
                    else
                    {
                        Console.WriteLine($"Game ID already exists in: {filePath}");
                    }
                }
                else
                {
                    // Create the file and write the new game ID
                    File.WriteAllText(filePath, info.Id.ToString() + "\n", System.Text.Encoding.UTF8);
                    Console.WriteLine($"Game ID saved to: {filePath}");
                }
                }
             catch (Exception ex)
            {
            Console.WriteLine($"Failed to save game ID: {ex.Message}");
        }
    }
    else
    {
        Console.WriteLine("Could not fetch the current SteamID64.");
    }

    try
    {
        Process.Start("SAM.Game.exe", info.Id.ToString(CultureInfo.InvariantCulture));
    }
    catch (Win32Exception)
    {
        MessageBox.Show(
            this,
            "Failed to start SAM.Game.exe.",
            "Error",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}

        private void OnRefresh(object sender, EventArgs e)
        {
            this._AddGameTextBox.Text = "";
            this.AddGames();
        }

        private void OnAddGame(object sender, EventArgs e)
        {
            uint id;

            if (uint.TryParse(this._AddGameTextBox.Text, out id) == false)
            {
                MessageBox.Show(
                    this,
                    "Please enter a valid game ID.",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            if (this.OwnsGame(id) == false)
            {
                MessageBox.Show(this, "You don't own that game.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            this._AddGameTextBox.Text = "";
            this._Games.Clear();
            this.AddGame(id, "normal");
            this._FilterGamesMenuItem.Checked = true;
            this.RefreshGames();
            this.DownloadNextLogo();
        }

        private void OnFilterUpdate(object sender, EventArgs e)
        {
            this.RefreshGames();
        }

        private async void unlockAllGames_Click(object sender, EventArgs e)
        {
            // Check if save directory exists
            string currentSteamId = GetCurrentSteamId();
            if (currentSteamId == null)
            {
                Console.WriteLine("Could not fetch the current SteamID64.");
                return;
            }

            string saveDirectory = Path.Combine(".", "data", "saves", currentSteamId);
            if (!Directory.Exists(saveDirectory))
            {
                Console.WriteLine("Save directory does not exist.");
                return;
            }

            string saveFilePath = Path.Combine(saveDirectory, "save.unlocks");

            // Initialize a HashSet to store unlocked game IDs
            HashSet<string> unlockedGameIds = new HashSet<string>();

            // Check if save file exists; if not, create it
            if (File.Exists(saveFilePath))
            {
                // Read existing unlocked games into the HashSet for efficient lookups
                unlockedGameIds.UnionWith(File.ReadAllLines(saveFilePath));
            }
            else
            {
                // Create the save file if it doesn't exist
                try
                {
                    using (File.Create(saveFilePath)) { } // Create and immediately close the file
                    Console.WriteLine("Save file created: " + saveFilePath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to create save file: {ex.Message}");
                    return; // Exit if we cannot create the file
                }
            }

            if (MessageBox.Show(
                "This will open and close A LOT of windows.\n\nIn your case, it could be " + Games.Count + " windows.\n\nThanks to the fix it won't be an issue.\n\nMake sure to run kill_all.exe when you finished unlocking your achievements!",
                "Warning",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.No)
            {
                unlockAllProgress.Visible = true;
                unlockAllProgress.Value = 0;
                unlockAllProgress.Maximum = Games.Count;

                foreach (var game in Games)
                {
                    // Check if game is already unlocked
                    if (unlockedGameIds.Contains(game.Id.ToString()))
                    {
                        Console.WriteLine($"Game {game.Id} is already unlocked. Skipping...");
                        unlockAllProgress.Value++;
                        continue;
                    }

                    unlockAllProgress.Value++;
                    try
                    {
                        var process = Process.Start("SAM.Game.exe", game.Id.ToString(CultureInfo.InvariantCulture) + " auto");

                        if (process != null && !process.HasExited)
                        {
                            await Task.Delay(3000);
                            if (!process.HasExited)
                            {
                                process.Kill();
                            }
                        }

                        // Append game ID to save file
                        try
                        {
                            File.AppendAllText(saveFilePath, game.Id.ToString() + Environment.NewLine, System.Text.Encoding.UTF8);
                            Console.WriteLine($"Game ID saved to: {saveFilePath}");
                            unlockedGameIds.Add(game.Id.ToString()); // Add to HashSet to prevent future duplicates
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to save game ID: {ex.Message}");
                        }
                    }
                    catch (Win32Exception)
                    {
                        MessageBox.Show(
                            this,
                            "Failed to start SAM.Game.exe.",
                            "Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                    catch (InvalidOperationException)
                    {
                        // Ignore if the process has already exited
                    }
                }
                unlockAllProgress.Visible = false;
            }
        }

        private void GamePicker_Load(object sender, EventArgs e)
        {

        }
    }
}