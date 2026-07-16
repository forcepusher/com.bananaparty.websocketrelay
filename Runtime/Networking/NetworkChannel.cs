using System.Collections.Generic;
using UnityEngine;

namespace BananaParty.WebSocketRelay
{
    [CreateAssetMenu]
    public class NetworkChannel : ScriptableObject
    {
        // Runtime-only. Scene bindings must not be serialized into the asset.
        private readonly List<NetworkBinding> _channelBindings = new();

        public void AddBinding(NetworkBinding binding)
        {
            _channelBindings.Add(binding);
        }

        public void RemoveBinding(NetworkBinding binding)
        {
            _channelBindings.Remove(binding);
        }

        public void SetChannel(string channel)
        {
            foreach (NetworkBinding binding in _channelBindings)
                binding.SetBinding(channel);
        }
    }
}
