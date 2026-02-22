using System;
using System.Linq;
using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Color = Microsoft.Xna.Framework.Color;

namespace GPadder
{
    public class GamepadSettingsView : View
    {
        private readonly GamepadManager _gamepadManager;
        
        private Dropdown _gamepadDropdown;
        private Checkbox _autoSwitchCheckbox;
        private Label _statusLabel;
        private FlowPanel _inputPanel;
        private Label[] _buttonLabels;
        private Label _stickLabel;
        private Label _triggerLabel;
        private Label _extraButtonsLabel;

        public GamepadSettingsView(GamepadManager gamepadManager)
        {
            _gamepadManager = gamepadManager;
        }

        protected override void Build(Container buildPanel)
        {
            var rootPanel = new FlowPanel
            {
                Size = buildPanel.Size,
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                ControlPadding = new Vector2(0, 10),
                Parent = buildPanel
            };

            // Gamepad Selection
            var selectionPanel = new FlowPanel
            {
                WidthSizingMode = SizingMode.Fill,
                HeightSizingMode = SizingMode.AutoSize,
                FlowDirection = ControlFlowDirection.LeftToRight,
                ControlPadding = new Vector2(10, 0),
                Parent = rootPanel
            };

            new Label
            {
                Text = "Select Gamepad:",
                AutoSizeWidth = true,
                Parent = selectionPanel
            };

            _gamepadDropdown = new Dropdown
            {
                Width = 250,
                Parent = selectionPanel
            };

            RefreshGamepadList();

            _gamepadDropdown.ValueChanged += (s, e) =>
            {
                var parts = e.CurrentValue.Split(' ');
                if (parts.Length > 0 && int.TryParse(parts[0], out var index))
                {
                    _gamepadManager.SelectGamepad(index);
                }
            };

            _autoSwitchCheckbox = new Checkbox
            {
                Text = "Auto-switch Gamepad",
                Checked = _gamepadManager.AutoSwitch,
                Parent = selectionPanel
            };
            _autoSwitchCheckbox.CheckedChanged += (s, e) =>
            {
                _gamepadManager.AutoSwitch = e.Checked;
            };

            var refreshButton = new StandardButton
            {
                Text = "Refresh List",
                Width = 100,
                Parent = selectionPanel
            };
            refreshButton.Click += (s, e) => RefreshGamepadList();

            // Subscribe to connection events
            _gamepadManager.GamepadConnected += (s, e) => RefreshGamepadList();
            _gamepadManager.GamepadDisconnected += (s, e) => RefreshGamepadList();
            _gamepadManager.SelectedGamepadChanged += (s, e) => SyncDropdown(e.PlayerIndex);

            // Status
            _statusLabel = new Label
            {
                Text = _gamepadManager.IsConnected ? "Status: Connected" : "Status: Disconnected",
                TextColor = _gamepadManager.IsConnected ? Color.Green : Color.Red,
                AutoSizeWidth = true,
                Parent = rootPanel
            };

            // Rumble Test
            var rumblePanel = new FlowPanel
            {
                Title = "Rumble Test",
                WidthSizingMode = SizingMode.Fill,
                HeightSizingMode = SizingMode.AutoSize,
                FlowDirection = ControlFlowDirection.LeftToRight,
                ControlPadding = new Vector2(10, 0),
                CanCollapse = true,
                Parent = rootPanel
            };

            var lowFreqButton = new StandardButton
            {
                Text = "Test Low Freq (Large)",
                Width = 150,
                Parent = rumblePanel
            };
            lowFreqButton.Click += (s, e) => _gamepadManager.SetRumble(0.5f, 0f, TimeSpan.FromMilliseconds(500));

            var highFreqButton = new StandardButton
            {
                Text = "Test High Freq (Small)",
                Width = 150,
                Parent = rumblePanel
            };
            highFreqButton.Click += (s, e) => _gamepadManager.SetRumble(0f, 0.5f, TimeSpan.FromMilliseconds(500));

            var bothButton = new StandardButton
            {
                Text = "Test Both",
                Width = 100,
                Parent = rumblePanel
            };
            bothButton.Click += (s, e) => _gamepadManager.SetRumble(0.5f, 0.5f, TimeSpan.FromMilliseconds(500));

            // Input Display
            _inputPanel = new FlowPanel
            {
                Title = "Input Monitor",
                WidthSizingMode = SizingMode.Fill,
                HeightSizingMode = SizingMode.AutoSize,
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                CanCollapse = true,
                Parent = rootPanel
            };

            _stickLabel = new Label { Text = "Sticks: ", AutoSizeWidth = true, Parent = _inputPanel };
            _triggerLabel = new Label { Text = "Triggers: ", AutoSizeWidth = true, Parent = _inputPanel };
            _extraButtonsLabel = new Label { Text = "Extra Buttons: None", AutoSizeWidth = true, Parent = _inputPanel };
            
            _buttonLabels = new Label[15];
            var buttons = Enum.GetValues(typeof(Buttons)).Cast<Buttons>().Take(15).ToList();
            for (int i = 0; i < buttons.Count; i++)
            {
                _buttonLabels[i] = new Label
                {
                    Text = buttons[i].ToString(),
                    TextColor = Color.Gray,
                    AutoSizeWidth = true,
                    Parent = _inputPanel
                };
            }
        }

        private void RefreshGamepadList()
        {
            _gamepadDropdown.Items.Clear();
            foreach (var index in _gamepadManager.GetConnectedGamepads())
            {
                var name = _gamepadManager.GetGamepadName(index);
                _gamepadDropdown.Items.Add($"{index} ({name})");
            }

            SyncDropdown(_gamepadManager.SelectedIndex);
        }

        private void SyncDropdown(int index)
        {
            var item = _gamepadDropdown.Items.FirstOrDefault(i => i.StartsWith(index.ToString()));
            if (item != null)
            {
                _gamepadDropdown.SelectedItem = item;
            }
        }

        public void UpdateView(GameTime gameTime)
        {
            if (_statusLabel == null) return;

            var activeIndex = _gamepadManager.SelectedIndex;
            _statusLabel.Text = _gamepadManager.IsConnected 
                ? $"Status: Connected on index {activeIndex}" 
                : $"Status: Disconnected (index {activeIndex} selected)";
            _statusLabel.TextColor = _gamepadManager.IsConnected ? Color.Green : Color.Red;

            if (_gamepadManager.IsConnected)
            {
                var state = _gamepadManager.CurrentState;
                _stickLabel.Text = $"Sticks: L: {state.ThumbSticks.Left} R: {state.ThumbSticks.Right}";
                _triggerLabel.Text = $"Triggers: L: {state.Triggers.Left:P0} R: {state.Triggers.Right:P0}";

                var buttons = Enum.GetValues(typeof(Buttons)).Cast<Buttons>().ToList();
                for (int i = 0; i < _buttonLabels.Length && i < buttons.Count; i++)
                {
                    bool isPressed = state.IsButtonDown(buttons[i]);
                    _buttonLabels[i].TextColor = isPressed ? Color.White : Color.Gray;
                }

                var extraButtons = _gamepadManager.GetPressedJoystickButtons().ToList();
                if (extraButtons.Any())
                {
                    _extraButtonsLabel.Text = $"Extra Buttons: {string.Join(", ", extraButtons)}";
                    _extraButtonsLabel.TextColor = Color.White;
                }
                else
                {
                    _extraButtonsLabel.Text = "Extra Buttons: None";
                    _extraButtonsLabel.TextColor = Color.Gray;
                }
            }
        }
    }
}
