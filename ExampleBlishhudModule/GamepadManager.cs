using System;
using System.Collections.Generic;
using Blish_HUD;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Threading.Tasks;

namespace GPadder
{
    public class GamepadManager : IDisposable
    {
        private static readonly Logger Logger = Logger.GetLogger<GamepadManager>();

        private PlayerIndex _currentPlayerIndex = PlayerIndex.One;
        private GamePadState _previousState;
        private GamePadState _currentState;

        public event EventHandler<GamepadEventArgs> GamepadConnected;
        public event EventHandler<GamepadEventArgs> GamepadDisconnected;

        public bool IsConnected => _currentState.IsConnected;
        public GamePadState CurrentState => _currentState;
        public PlayerIndex SelectedPlayerIndex => _currentPlayerIndex;

        public GamepadManager()
        {
            _currentState = GamePad.GetState(_currentPlayerIndex);
            _previousState = _currentState;
        }

        public void Update(GameTime gameTime)
        {
            _previousState = _currentState;
            _currentState = GamePad.GetState(_currentPlayerIndex);

            if (_currentState.IsConnected != _previousState.IsConnected)
            {
                if (_currentState.IsConnected)
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
        }

        public void SelectGamepad(PlayerIndex index)
        {
            _currentPlayerIndex = index;
            _currentState = GamePad.GetState(_currentPlayerIndex);
            _previousState = _currentState;
            Logger.Info($"Selected gamepad index: {_currentPlayerIndex}");
        }

        public IEnumerable<PlayerIndex> GetConnectedGamepads()
        {
            for (int i = 0; i < 4; i++)
            {
                var index = (PlayerIndex)i;
                if (GamePad.GetState(index).IsConnected)
                {
                    yield return index;
                }
            }
        }

        public void SetRumble(float leftMotor, float rightMotor, TimeSpan duration)
        {
            if (!IsConnected) return;

            GamePad.SetVibration(_currentPlayerIndex, leftMotor, rightMotor);
            
            if (duration > TimeSpan.Zero)
            {
                Task.Delay(duration).ContinueWith(_ => 
                {
                    GamePad.SetVibration(_currentPlayerIndex, 0, 0);
                });
            }
        }

        public void Dispose()
        {
            if (IsConnected)
            {
                GamePad.SetVibration(_currentPlayerIndex, 0, 0);
            }
        }
    }

    public class GamepadEventArgs : EventArgs
    {
        public PlayerIndex PlayerIndex { get; }
        public GamepadEventArgs(PlayerIndex playerIndex)
        {
            PlayerIndex = playerIndex;
        }
    }
}
