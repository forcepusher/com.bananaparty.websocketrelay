using System;

namespace BananaParty.WebSocketRelay
{
    public static class Hash
    {
        public static int StringToInt(string value)
        {
            if (string.IsNullOrEmpty(value))
                return 0;

            unchecked
            {
                int hash = 17;
                foreach (char character in value)
                    hash = hash * 31 + character;

                return hash;
            }
        }
    }
}
