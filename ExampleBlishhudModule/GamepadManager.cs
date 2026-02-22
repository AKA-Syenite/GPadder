using System;
using System.Collections.Generic;
using Blish_HUD;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Threading.Tasks;
using System.Linq;

namespace GPadder
{
    public class GamepadManager : IDisposable
    {
        private static readonly Logger Logger = Logger.GetLogger<GamepadManager>();

        private int _currentPlayerIndex = 0;
        private GamePadState _previousState;
        private GamePadState _currentState;

        private static int MaxGamepads = 8;
        
        private JoystickState _previousJoystickState;
        private JoystickState _currentJoystickState;

        public event EventHandler<GamepadEventArgs> GamepadConnected;
        public event EventHandler<GamepadEventArgs> GamepadDisconnected;
        public event EventHandler<GamepadEventArgs> SelectedGamepadChanged;

        public bool IsConnected => _currentState.IsConnected || _currentJoystickState.IsConnected;
        public GamePadState CurrentState => _currentState;
        public int SelectedIndex => _currentPlayerIndex;
        public bool AutoSwitch { get; set; } = false;

        public GamepadManager()
        {
            var connected = GetConnectedGamepads().ToList();
            Logger.Info($"GamepadManager initialized. Found {connected.Count} connected gamepads: {string.Join(", ", connected)}");
            
            if (connected.Any())
            {
                _currentPlayerIndex = connected.First();
                Logger.Info($"Defaulting to first connected gamepad: {_currentPlayerIndex}");
            }
            else
            {
                Logger.Info("No gamepads found at startup. Defaulting to index 0");
                _currentPlayerIndex = 0;
            }

            _currentState = GamePad.GetState(_currentPlayerIndex);
            _previousState = _currentState;
            
            _currentJoystickState = Joystick.GetState((int)_currentPlayerIndex);
            _previousJoystickState = _currentJoystickState;
        }

        public void Update(GameTime gameTime)
        {
            _previousState = _currentState;
            _currentState = GamePad.GetState(_currentPlayerIndex);

            _previousJoystickState = _currentJoystickState;
            _currentJoystickState = Joystick.GetState(_currentPlayerIndex);

            bool wasConnected = _previousState.IsConnected || _previousJoystickState.IsConnected;
            bool isConnected = _currentState.IsConnected || _currentJoystickState.IsConnected;

            if (isConnected != wasConnected)
            {
                if (isConnected)
                {
                    Logger.Info($"Gamepad connected on index {_currentPlayerIndex}");
                    GamepadConnected?.Invoke(this, new GamepadEventArgs(_currentPlayerIndex));
                }
                else
                {
                    Logger.Info($"Gamepad disconnected on index {_currentPlayerIndex}");
                    GamepadDisconnected?.Invoke(this, new GamepadEventArgs(_currentPlayerIndex));
                }
            }

            if (gameTime.TotalGameTime.TotalMilliseconds % 2000 < 50) // Roughly every 2 seconds
            {
                CheckForNewConnections();
            }

            if (AutoSwitch)
            {
                DetectInputAndSwitch();
            }
        }

        private void DetectInputAndSwitch()
        {
            for (int i = 0; i < MaxGamepads; i++)
            {
                var state = GamePad.GetState(i);
                var joystickState = Joystick.GetState(i);

                if ((state.IsConnected && HasSignificantInput(state)) || (joystickState.IsConnected && HasSignificantJoystickInput(joystickState)))
                {
                    if (_currentPlayerIndex != i)
                    {
                        Logger.Info($"Detected input on gamepad {i}. Auto-switching.");
                        SelectGamepad(i);
                    }
                    break;
                }
            }
        }

        private bool HasSignificantJoystickInput(JoystickState state)
        {
            if (!state.IsConnected) return false;

            // Check buttons
            for (int i = 0; i < state.Buttons.Length; i++)
            {
                if (state.Buttons[i] == ButtonState.Pressed) return true;
            }

            // Check Axes (with deadzone)
            for (int i = 0; i < state.Axes.Length; i++)
            {
                if (Math.Abs(state.Axes[i]) > 0.2f) return true;
            }

            // Check Hats
            for (int i = 0; i < state.Hats.Length; i++)
            {
                if (state.Hats[i].Up == ButtonState.Pressed || state.Hats[i].Down == ButtonState.Pressed ||
                    state.Hats[i].Left == ButtonState.Pressed || state.Hats[i].Right == ButtonState.Pressed)
                    return true;
            }

            return false;
        }

        private bool HasSignificantInput(GamePadState state)
        {
            // Check buttons
            if (state.Buttons.A == ButtonState.Pressed || state.Buttons.B == ButtonState.Pressed ||
                state.Buttons.X == ButtonState.Pressed || state.Buttons.Y == ButtonState.Pressed ||
                state.Buttons.Start == ButtonState.Pressed || state.Buttons.Back == ButtonState.Pressed ||
                state.Buttons.LeftShoulder == ButtonState.Pressed || state.Buttons.RightShoulder == ButtonState.Pressed ||
                state.Buttons.LeftStick == ButtonState.Pressed || state.Buttons.RightStick == ButtonState.Pressed)
                return true;

            // Check D-Pad
            if (state.DPad.Up == ButtonState.Pressed || state.DPad.Down == ButtonState.Pressed ||
                state.DPad.Left == ButtonState.Pressed || state.DPad.Right == ButtonState.Pressed)
                return true;

            // Check Triggers (with small deadzone)
            if (state.Triggers.Left > 0.1f || state.Triggers.Right > 0.1f)
                return true;

            // Check Thumbsticks (with small deadzone)
            if (state.ThumbSticks.Left.Length() > 0.2f || state.ThumbSticks.Right.Length() > 0.2f)
                return true;

            // Check Joystick buttons (for non-standard/HID buttons)
            if (_currentJoystickState.IsConnected)
            {
                for (int i = 0; i < _currentJoystickState.Buttons.Length; i++)
                {
                    if (_currentJoystickState.Buttons[i] == ButtonState.Pressed)
                        return true;
                }
            }

            return false;
        }

        public IEnumerable<int> GetPressedJoystickButtons()
        {
            if (!_currentJoystickState.IsConnected) yield break;

            for (int i = 0; i < _currentJoystickState.Buttons.Length; i++)
            {
                if (_currentJoystickState.Buttons[i] == ButtonState.Pressed)
                    yield return i;
            }
        }

        private void CheckForNewConnections()
        {
            for (int i = 0; i < MaxGamepads; i++)
            {
                if (i == _currentPlayerIndex) continue;

                var state = GamePad.GetState(i);
                var joystickState = Joystick.GetState(i);
                
                if (state.IsConnected || joystickState.IsConnected)
                {
                    // If we are currently disconnected, we might want to auto-switch
                    if (!IsConnected)
                    {
                        Logger.Info($"Found new gamepad on index {i} while current is disconnected. Auto-switching.");
                        SelectGamepad(i);
                        GamepadConnected?.Invoke(this, new GamepadEventArgs(i));
                        break;
                    }
                }
            }
        }

        public void SelectGamepad(int index)
        {
            if (_currentPlayerIndex == index) return;

            _currentPlayerIndex = index;
            _currentState = GamePad.GetState(_currentPlayerIndex);
            _previousState = _currentState;
            
            _currentJoystickState = Joystick.GetState(_currentPlayerIndex);
            _previousJoystickState = _currentJoystickState;

            Logger.Info($"Selected gamepad index: {_currentPlayerIndex}");
            SelectedGamepadChanged?.Invoke(this, new GamepadEventArgs(_currentPlayerIndex));
        }

        public string GetGamepadName(int index)
        {
            var caps = GamePad.GetCapabilities(index);
            if (!string.IsNullOrEmpty(caps.DisplayName)) return caps.DisplayName;

            var joystickCaps = Joystick.GetCapabilities(index);
            if (!string.IsNullOrEmpty(joystickCaps.DisplayName)) return joystickCaps.DisplayName;

            return joystickCaps.IsConnected ? $"Joystick {index}" : (caps.IsConnected ? $"Gamepad {index}" : "Unknown Controller");
        }

        public IEnumerable<int> GetConnectedGamepads()
        {
            for (int i = 0; i < MaxGamepads; i++)
            {
                if (GamePad.GetState(i).IsConnected || Joystick.GetCapabilities(i).IsConnected)
                {
                    yield return i;
                }
            }
        }

        public void SetRumble(float leftMotor, float rightMotor, TimeSpan duration)
        {
            if (!IsConnected || _currentPlayerIndex >= 4) return; // Rumble only works for XInput/PlayerIndex 0-3

            GamePad.SetVibration((PlayerIndex)_currentPlayerIndex, leftMotor, rightMotor);
            
            if (duration > TimeSpan.Zero)
            {
                Task.Delay(duration).ContinueWith(_ => 
                {
                    GamePad.SetVibration((PlayerIndex)_currentPlayerIndex, 0, 0);
                });
            }
        }

        public void Dispose()
        {
            if (IsConnected && _currentPlayerIndex < 4)
            {
                GamePad.SetVibration((PlayerIndex)_currentPlayerIndex, 0, 0);
            }
        }
    }

    public class GamepadEventArgs : EventArgs
    {
        public int PlayerIndex { get; }
        public GamepadEventArgs(int playerIndex)
        {
            PlayerIndex = playerIndex;
        }
    }
}
