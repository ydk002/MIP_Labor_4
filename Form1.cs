using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Labor3
{
    public partial class Form1 : Form
    {
        private const string DefaultHomePage = "https://www.bing.com/";
        private const string BlockedKeywordsFileName = "BlockedKeywords.txt";
        private const string DatabaseFileName = "users.db";
        
        // Async logger instance
        private AsyncLogger _logger;
        
        // List of blocked keywords - URLs containing these will be blocked
        private readonly List<string> blockedKeywords = new List<string>();
        
        // Database connection state
        private SQLiteManager _databaseConnection;
        private bool _isDatabaseConnected = false;

        public Form1()
        {
            InitializeComponent();
            
            // Configure async logger
            ConfigureAsyncLogger();
            
            // Load blocked keywords from file
            LoadBlockedKeywords();
            
            // Suppress script errors
            webBrowser1.ScriptErrorsSuppressed = true;
            
            // Wire up event handlers
            this.btnGo.Click += btnGo_Click;
            this.btnBack.Click += btnBack_Click;
            this.btnForward.Click += btnForward_Click;
            this.btnHome.Click += btnHome_Click;
            this.Load += Form1_Load;
            this.txtUrl.KeyDown += txtUrl_KeyDown;
            
            // Wire up blocked keyword management
            this.toolStripButton1.Click += toolStripButton1_Click;
            this.toolStripTextBox1.KeyDown += toolStripTextBox1_KeyDown;
            
            // Wire up SQLite test button
            this.btnSqliteTest.Click += btnSqliteTest_Click;
            
            // Wire up menu items
            this.connectToolStripMenuItem.Click += connectToolStripMenuItem_Click;
            this.disconnectToolStripMenuItem.Click += disconnectToolStripMenuItem_Click;
            this.addKeywordToolStripMenuItem.Click += addKeywordToolStripMenuItem_Click;
            this.viewKeywordsToolStripMenuItem.Click += viewKeywordsToolStripMenuItem_Click;
            this.aboutToolStripMenuItem.Click += aboutToolStripMenuItem_Click;
            
            // Subscribe to navigation events
            this.webBrowser1.Navigated += webBrowser1_Navigated;
            this.webBrowser1.Navigating += webBrowser1_Navigating;
            
            LogEvent("Application started");
        }

        #region Menu Event Handlers

        private void connectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LogEvent("Menu: Connect to database clicked");
            
            try
            {
                if (_isDatabaseConnected)
                {
                    MessageBox.Show("Database is already connected.", "Already Connected",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                string databasePath = Path.Combine(Application.StartupPath, DatabaseFileName);
                _databaseConnection = new SQLiteManager(databasePath, _logger);
                _isDatabaseConnected = true;

                // Update menu states
                connectToolStripMenuItem.Enabled = false;
                disconnectToolStripMenuItem.Enabled = true;

                LogEvent($"Database connected: {databasePath}");
                MessageBox.Show($"Successfully connected to database:\n{databasePath}", 
                    "Database Connected", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                LogEvent($"ERROR: Failed to connect to database - {ex.Message}");
                MessageBox.Show($"Failed to connect to database:\n{ex.Message}", 
                    "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void disconnectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LogEvent("Menu: Disconnect from database clicked");
            
            try
            {
                if (!_isDatabaseConnected)
                {
                    MessageBox.Show("No database connection to disconnect.", "Not Connected",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                _databaseConnection?.Dispose();
                _databaseConnection = null;
                _isDatabaseConnected = false;

                // Update menu states
                connectToolStripMenuItem.Enabled = true;
                disconnectToolStripMenuItem.Enabled = false;

                LogEvent("Database disconnected");
                MessageBox.Show("Successfully disconnected from database.", 
                    "Database Disconnected", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                LogEvent($"ERROR: Failed to disconnect from database - {ex.Message}");
                MessageBox.Show($"Error disconnecting from database:\n{ex.Message}", 
                    "Disconnection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void addKeywordToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LogEvent("Menu: Add Keyword clicked");
            
            // Create a simple input dialog
            Form inputDialog = new Form
            {
                Text = "Add Blocked Keyword",
                Width = 400,
                Height = 150,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            Label label = new Label
            {
                Text = "Enter keyword to block:",
                Left = 10,
                Top = 20,
                Width = 350
            };

            TextBox textBox = new TextBox
            {
                Left = 10,
                Top = 45,
                Width = 360
            };

            Button okButton = new Button
            {
                Text = "Add",
                Left = 210,
                Top = 75,
                DialogResult = DialogResult.OK
            };

            Button cancelButton = new Button
            {
                Text = "Cancel",
                Left = 290,
                Top = 75,
                DialogResult = DialogResult.Cancel
            };

            inputDialog.Controls.AddRange(new Control[] { label, textBox, okButton, cancelButton });
            inputDialog.AcceptButton = okButton;
            inputDialog.CancelButton = cancelButton;

            if (inputDialog.ShowDialog(this) == DialogResult.OK)
            {
                string keyword = textBox.Text.Trim().ToLower();

                if (string.IsNullOrWhiteSpace(keyword))
                {
                    LogEvent("Menu Add Keyword: Empty keyword provided");
                    MessageBox.Show("Please enter a keyword to block.", "Empty Keyword",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (blockedKeywords.Any(k => k.Equals(keyword, StringComparison.OrdinalIgnoreCase)))
                {
                    LogEvent($"Menu Add Keyword: '{keyword}' already exists");
                    MessageBox.Show($"The keyword '{keyword}' is already in the block list.",
                        "Duplicate Keyword", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                blockedKeywords.Add(keyword);
                SaveBlockedKeywords();
                UpdateBlockedKeywordsComboBox();

                LogEvent($"Menu Add Keyword: '{keyword}' added successfully");
                MessageBox.Show($"Keyword '{keyword}' has been added to the block list.",
                    "Keyword Added", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void viewKeywordsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LogEvent("Menu: View Keywords clicked");
            
            // Create a form to display keywords
            Form viewForm = new Form
            {
                Text = "Blocked Keywords",
                Width = 500,
                Height = 400,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.Sizable
            };

            ListBox listBox = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10)
            };

            // Add keywords to listbox
            var sortedKeywords = blockedKeywords.OrderBy(k => k).ToList();
            foreach (var keyword in sortedKeywords)
            {
                listBox.Items.Add(keyword);
            }

            if (listBox.Items.Count == 0)
            {
                listBox.Items.Add("(No blocked keywords)");
            }

            Panel buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 40
            };

            Button closeButton = new Button
            {
                Text = "Close",
                Width = 80,
                Height = 30,
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
                Left = viewForm.Width - 100,
                Top = 5
            };
            closeButton.Click += (s, ev) => viewForm.Close();

            buttonPanel.Controls.Add(closeButton);
            viewForm.Controls.Add(listBox);
            viewForm.Controls.Add(buttonPanel);

            LogEvent($"Displaying {blockedKeywords.Count} blocked keywords");
            viewForm.ShowDialog(this);
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LogEvent("Menu: About clicked");
            
            string aboutMessage = "Web Browser with Content Filtering\n\n" +
                                 "Version: 1.0.0\n" +
                                 "Framework: .NET Framework 4.7.2\n\n" +
                                 "Features:\n" +
                                 "• Web browsing with keyword-based content filtering\n" +
                                 "• SQLite database integration\n" +
                                 "• Async logging system\n" +
                                 "• Blocked keyword management\n\n" +
                                 "© 2025 Labor3 Project";

            MessageBox.Show(aboutMessage, "About Web Browser",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        #endregion

        private void ConfigureAsyncLogger()
        {
            // Create log file path in the application directory
            string logFilePath = System.IO.Path.Combine(
                Application.StartupPath, 
                $"BrowserLog_{DateTime.Now:yyyyMMdd}.txt");
            
            // Create async logger instance
            _logger = new AsyncLogger(logFilePath);
        }

        private async void btnSqliteTest_Click(object sender, EventArgs e)
        {
            LogEvent("SQLite Test button clicked");

            try
            {
                // Get database path
                string databasePath = Path.Combine(Application.StartupPath, DatabaseFileName);

                LogEvent($"Starting SQLite database operations using System.Data.SQLite");
                LogEvent($"Database path: {databasePath}");
                
                // Show progress message
                this.Cursor = Cursors.WaitCursor;
                this.Enabled = false;

                // Create SQLite manager and perform operations
                using (SQLiteManager sqliteManager = new SQLiteManager(databasePath, _logger))
                {
                    string result = await sqliteManager.PerformDatabaseTasksAsync();
                    
                    // Show results in a message box with scrollable view
                    Form resultForm = new Form
                    {
                        Text = "SQLite Database Operations Completed",
                        Width = 600,
                        Height = 500,
                        StartPosition = FormStartPosition.CenterParent,
                        FormBorderStyle = FormBorderStyle.Sizable
                    };

                    TextBox textBox = new TextBox
                    {
                        Multiline = true,
                        ScrollBars = ScrollBars.Both,
                        Dock = DockStyle.Fill,
                        Font = new Font("Consolas", 9),
                        Text = result,
                        ReadOnly = true
                    };

                    resultForm.Controls.Add(textBox);
                    resultForm.ShowDialog(this);
                }

                LogEvent("SQLite operations completed successfully");
            }
            catch (Exception ex)
            {
                string errorMessage = $"Error during SQLite operations: {ex.Message}";
                LogEvent($"ERROR: {errorMessage}");
                MessageBox.Show($"{errorMessage}\n\nStack Trace:\n{ex.StackTrace}", 
                    "SQLite Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.Cursor = Cursors.Default;
                this.Enabled = true;
            }
        }

        private void LoadBlockedKeywords()
        {
            string filePath = Path.Combine(Application.StartupPath, BlockedKeywordsFileName);

            try
            {
                if (File.Exists(filePath))
                {
                    // Read all lines from the file
                    string[] keywords = File.ReadAllLines(filePath);
                    
                    // Use LINQ to filter out empty lines and add to list
                    var validKeywords = keywords
                        .Select(k => k.Trim())
                        .Where(k => !string.IsNullOrWhiteSpace(k))
                        .Distinct()
                        .ToList();
                    
                    blockedKeywords.AddRange(validKeywords);
                    
                    LogEvent($"Loaded {blockedKeywords.Count} blocked keywords from file");
                }
                else
                {
                    // Initialize with default keywords if file doesn't exist
                    blockedKeywords.AddRange(new List<string>
                    {
                        "facebook",
                        "gambling",
                        "casino",
                        "adult",
                        "nsfw"
                    });
                    
                    // Save the default keywords to file
                    SaveBlockedKeywords();
                    
                    LogEvent($"Initialized {blockedKeywords.Count} default blocked keywords");
                }
            }
            catch (Exception ex)
            {
                LogEvent($"ERROR: Failed to load blocked keywords. Exception: {ex.Message}");
                
                // Fallback to default keywords
                if (blockedKeywords.Count == 0)
                {
                    blockedKeywords.AddRange(new List<string>
                    {
                        "facebook",
                        "gambling",
                        "casino",
                        "adult",
                        "nsfw"
                    });
                }
            }
        }

        private void SaveBlockedKeywords()
        {
            string filePath = Path.Combine(Application.StartupPath, BlockedKeywordsFileName);

            try
            {
                // Use LINQ to sort keywords alphabetically before saving
                var sortedKeywords = blockedKeywords
                    .OrderBy(k => k)
                    .ToList();
                
                // Write all keywords to file (one per line)
                File.WriteAllLines(filePath, sortedKeywords);
                
                LogEvent($"Saved {blockedKeywords.Count} blocked keywords to file");
            }
            catch (Exception ex)
            {
                LogEvent($"ERROR: Failed to save blocked keywords. Exception: {ex.Message}");
                MessageBox.Show($"Failed to save blocked keywords: {ex.Message}",
                    "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LogEvent(string message)
        {
            // Log asynchronously without waiting
            _logger?.LogEvent(message);
        }

        private async void btnGo_Click(object sender, EventArgs e)
        {
            LogEvent($"Go button clicked - Navigating to: {txtUrl.Text.Trim()}");
            await NavigateToUrlAsync(txtUrl.Text.Trim());
        }

        private void btnBack_Click(object sender, EventArgs e)
        {
            LogEvent("Back button clicked");
            if (webBrowser1.CanGoBack)
            {
                webBrowser1.GoBack();
                LogEvent("Navigated back in history");
            }
            else
            {
                LogEvent("Cannot go back - no history available");
            }
        }

        private void btnForward_Click(object sender, EventArgs e)
        {
            LogEvent("Forward button clicked");
            if (webBrowser1.CanGoForward)
            {
                webBrowser1.GoForward();
                LogEvent("Navigated forward in history");
            }
            else
            {
                LogEvent("Cannot go forward - no history available");
            }
        }

        private async void btnHome_Click(object sender, EventArgs e)
        {
            LogEvent($"Home button clicked - Navigating to: {DefaultHomePage}");
            await NavigateToUrlAsync(DefaultHomePage);
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            LogEvent("Form loaded");
            
            // Populate the combo box with initial blocked keywords
            UpdateBlockedKeywordsComboBox();
            
            await NavigateToUrlAsync(DefaultHomePage);
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            AddBlockedKeyword();
        }

        private void toolStripTextBox1_KeyDown(object sender, KeyEventArgs e)
        {
            // Add keyword when Enter is pressed
            if (e.KeyCode == Keys.Enter)
            {
                AddBlockedKeyword();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void AddBlockedKeyword()
        {
            string keyword = toolStripTextBox1.Text.Trim().ToLower();

            // Validate input
            if (string.IsNullOrWhiteSpace(keyword))
            {
                LogEvent("Add blocked keyword failed: Empty keyword");
                MessageBox.Show("Please enter a keyword to block.", "Empty Keyword",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Check if keyword already exists using LINQ
            if (blockedKeywords.Any(k => k.Equals(keyword, StringComparison.OrdinalIgnoreCase)))
            {
                LogEvent($"Add blocked keyword failed: '{keyword}' already exists");
                MessageBox.Show($"The keyword '{keyword}' is already in the block list.",
                    "Duplicate Keyword", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Add keyword to the list
            blockedKeywords.Add(keyword);
            LogEvent($"Blocked keyword added: '{keyword}'");

            // Save keywords to file immediately
            SaveBlockedKeywords();

            // Update the combo box
            UpdateBlockedKeywordsComboBox();

            // Clear the textbox
            toolStripTextBox1.Text = string.Empty;

            // Show confirmation
            MessageBox.Show($"Keyword '{keyword}' has been added to the block list.",
                "Keyword Added", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void UpdateBlockedKeywordsComboBox()
        {
            // Clear and repopulate the combo box with blocked keywords using LINQ
            toolStripComboBox1.Items.Clear();
            
            var sortedKeywords = blockedKeywords.OrderBy(k => k).ToList();
            
            foreach (var keyword in sortedKeywords)
            {
                toolStripComboBox1.Items.Add(keyword);
            }

            // Select the first item if available
            if (toolStripComboBox1.Items.Count > 0)
            {
                toolStripComboBox1.SelectedIndex = 0;
            }
        }

        private void webBrowser1_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            // Update the URL textbox with the current URL
            if (webBrowser1.Url != null)
            {
                txtUrl.Text = webBrowser1.Url.ToString();
                LogEvent($"Document completed loading: {webBrowser1.Url}");
            }
            
            // Update navigation button states
            UpdateNavigationButtons();
        }

        private void webBrowser1_Navigated(object sender, WebBrowserNavigatedEventArgs e)
        {
            // Update the URL textbox when navigation occurs
            if (webBrowser1.Url != null)
            {
                txtUrl.Text = webBrowser1.Url.ToString();
                LogEvent($"Successfully navigated to: {webBrowser1.Url}");
            }
            
            // Update navigation button states
            UpdateNavigationButtons();
        }

        private void webBrowser1_Navigating(object sender, WebBrowserNavigatingEventArgs e)
        {
            LogEvent($"Attempting navigation to: {e.Url}");
            
            // Check if the URL contains any blocked keywords using LINQ
            string url = e.Url.ToString().ToLower();
            
            // Use LINQ to find if any blocked keyword exists in the URL
            var blockedKeyword = blockedKeywords
                .FirstOrDefault(keyword => url.Contains(keyword.ToLower()));
            
            if (blockedKeyword != null)
            {
                // Cancel the navigation
                e.Cancel = true;
                
                LogEvent($"BLOCKED: Navigation to '{e.Url}' blocked by keyword: '{blockedKeyword}'");
                
                // Show a message to the user
                MessageBox.Show(
                    $"Access to this website is blocked.\nBlocked keyword: '{blockedKeyword}'",
                    "Access Denied",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private async void txtUrl_KeyDown(object sender, KeyEventArgs e)
        {
            // Navigate when Enter key is pressed
            if (e.KeyCode == Keys.Enter)
            {
                LogEvent($"Enter key pressed in URL textbox - Navigating to: {txtUrl.Text.Trim()}");
                await NavigateToUrlAsync(txtUrl.Text.Trim());
                e.Handled = true;
                e.SuppressKeyPress = true; // Prevent the beep sound
            }
        }

        private async Task NavigateToUrlAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                LogEvent("Navigation failed: Empty URL provided");
                MessageBox.Show("Please enter a valid URL.", "Invalid URL", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Add protocol if missing
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                string originalUrl = url;
                url = "https://" + url;
                LogEvent($"Protocol added: '{originalUrl}' -> '{url}'");
            }

            // Check for blocked keywords before navigation using LINQ
            string urlLower = url.ToLower();
            var blockedKeyword = blockedKeywords
                .FirstOrDefault(keyword => urlLower.Contains(keyword.ToLower()));
            
            if (blockedKeyword != null)
            {
                LogEvent($"BLOCKED: Pre-navigation check blocked '{url}' by keyword: '{blockedKeyword}'");
                MessageBox.Show(
                    $"Access to this website is blocked.\nBlocked keyword: '{blockedKeyword}'",
                    "Access Denied",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            try
            {
                LogEvent($"Starting async navigation to: {url}");
                
                // Perform navigation asynchronously
                await Task.Run(() =>
                {
                    // Invoke on UI thread since WebBrowser control requires it
                    if (webBrowser1.InvokeRequired)
                    {
                        webBrowser1.Invoke(new Action(() => webBrowser1.Navigate(url)));
                    }
                    else
                    {
                        webBrowser1.Navigate(url);
                    }
                });
            }
            catch (UriFormatException ex)
            {
                LogEvent($"ERROR: Invalid URL format - {url}. Exception: {ex.Message}");
                MessageBox.Show("The URL format is invalid. Please check and try again.", 
                    "Invalid URL", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                LogEvent($"ERROR: Navigation failed - {url}. Exception: {ex.Message}");
                MessageBox.Show($"Navigation failed: {ex.Message}", 
                    "Navigation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateNavigationButtons()
        {
            // Enable/disable navigation buttons based on history
            btnBack.Enabled = webBrowser1.CanGoBack;
            btnForward.Enabled = webBrowser1.CanGoForward;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            LogEvent("Application closing");
            
            // Disconnect from database if connected
            if (_isDatabaseConnected)
            {
                _databaseConnection?.Dispose();
                _databaseConnection = null;
                LogEvent("Database connection closed during shutdown");
            }
            
            // Save blocked keywords one final time
            SaveBlockedKeywords();
            
            // Dispose logger to ensure all messages are written
            _logger?.Dispose();
            
            base.OnFormClosing(e);
        }
    }
}
