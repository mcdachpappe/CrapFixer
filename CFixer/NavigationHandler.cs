using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CFixer
{
    /// <summary>
    /// Handles navigation button highlighting for a set of UI buttons.
    /// </summary>
    public class NavigationHandler
    {
        // List of all buttons managed by the navigation handler
        private readonly List<Button> _buttons;

        // Color used for the background of the active button
        private readonly Color _activeBackgroundColor = Color.FromArgb(180, 150, 200, 240);

        // Color used for the background of inactive buttons (same as form)
        private readonly Color _inactiveBackgroundColor = Color.FromArgb(103, 103, 103);

        // Color used for the border of the active button
        private readonly Color _activeBorderColor = Color.FromArgb(255, 120, 170, 210);

        /// <summary>
        /// Event fired when a navigation button is clicked.
        /// </summary>
        public event Action<Button> NavigationButtonClicked;

        /// <summary>
        /// Initializes the handler with the buttons that should be managed.
        /// </summary>
        /// <param name="buttons">The buttons to include in the navigation group.</param>
        public NavigationHandler(params Button[] buttons)
        {
            _buttons = new List<Button>(buttons);

            foreach (var button in _buttons)
            {
                button.Click += OnButtonClick;
            }
        }

        private void OnButtonClick(object sender, EventArgs e)
        {
            if (sender is Button clickedButton)
            {
                SetActive(clickedButton);
                clickedButton.FindForm().ActiveControl = null;  // Remove focus so no extra border is drawn
                NavigationButtonClicked?.Invoke(clickedButton);
            }
        }

        /// <summary>
        /// Sets the given button as active and updates the appearance of all buttons.
        /// </summary>
        /// <param name="activeButton">The button to mark as active.</param>
        public void SetActive(Button activeButton)
        {
            foreach (var button in _buttons)
            {
                // Skip GitHub button to avoid conflicts with its custom behavior
                if (button.Name.Equals("btnGitHub", StringComparison.OrdinalIgnoreCase))
                    continue;

                bool isActive = button == activeButton;

                if (isActive)
                {
                    button.BackColor = _activeBackgroundColor;
                    button.FlatAppearance.BorderColor = _activeBorderColor;
                    button.FlatAppearance.BorderSize = 1;
                }
                else
                {
                    button.BackColor = _inactiveBackgroundColor;
                    button.FlatAppearance.BorderSize = 0;
                }

                button.ForeColor = Color.WhiteSmoke;
            }
        }

        /// <summary>
        /// Loads and assigns icons to buttons asynchronously based on their names.
        /// </summary>
        /// <param name="iconFolder">The relative folder path where the icon files are located. Defaults to "icons". Each button's icon file name is
        /// derived from the button's name by removing the "btn" prefix, converting the remainder to lowercase, and
        /// appending ".png".</param>
        public async Task LoadNavigationIcons(string iconFolder = "icons")
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string fullIconPath = Path.Combine(basePath, iconFolder);
            if (!Directory.Exists(fullIconPath)) return;

            // Create graphics context to get system DPI scaling (default is 96 DPI)
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
            {
                float dpiScale = g.DpiX / 96f;
                int defaultIconSize = (int)(32 * dpiScale);     // Scale 32px base size with DPI
                int githubIconSize = (int)(42 * dpiScale);       // Special size for GitHub button

                foreach (var button in _buttons)
                {
                    // Convert button name like "btnHome" > "home.png"
                    string buttonName = button.Name.ToLower();
                    string fileName;

                    if (buttonName == "btngithub")
                        fileName = "github.png";
                    else if (buttonName.StartsWith("btn"))
                        fileName = buttonName.Replace("btn", "") + ".png";
                    else
                        continue;

                    string filePath = Path.Combine(fullIconPath, fileName);

                    if (!File.Exists(filePath))
                        continue;

                    try
                    {
                        // Read file bytes asynchronously off the UI thread
                        byte[] imageData = await Task.Run(() => File.ReadAllBytes(filePath));

                        using (var ms = new MemoryStream(imageData))
                        using (Image original = Image.FromStream(ms))
                        {
                            int iconSize = buttonName == "btngithub" ? githubIconSize : defaultIconSize;

                            Bitmap resized = new Bitmap(iconSize, iconSize);
                            try
                            {
                                using (Graphics gr = Graphics.FromImage(resized))
                                {
                                    gr.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                                    gr.DrawImage(original, 0, 0, iconSize, iconSize);
                                }

                                // Set button image
                                button.Image = resized;
                                button.ImageAlign = ContentAlignment.TopCenter;
                                button.TextAlign = ContentAlignment.BottomCenter;
                            }
                            catch
                            {
                                resized.Dispose(); // Cleanup if drawing fails
                                throw;
                            }
                        }
                    }
                    catch
                    {
                        // Optional: log or ignore loading errors
                    }
                }
            }
        }
    }
}