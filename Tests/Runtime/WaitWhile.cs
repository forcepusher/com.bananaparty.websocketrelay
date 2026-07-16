using System;
using System.Diagnostics;
using UnityEngine;

namespace BananaParty.WebSocketRelay.Tests
{
    public class WaitWhile : CustomYieldInstruction
    {
        private readonly Func<bool> _conditionFunc;
        private readonly float _timeoutThreshold;

        private readonly Stopwatch _stopwatch = new();

        public WaitWhile(Func<bool> condition, float timeoutThreshold = float.PositiveInfinity)
        {
            _conditionFunc = condition;
            _timeoutThreshold = timeoutThreshold;
        }

        public override bool keepWaiting
        {
            get
            {
                if (!_stopwatch.IsRunning)
                    _stopwatch.Start();

                return _conditionFunc.Invoke() && _stopwatch.Elapsed.TotalSeconds < _timeoutThreshold;
            }
        }

        public override void Reset()
        {
            base.Reset();

            _stopwatch.Reset();
        }
    }
}
