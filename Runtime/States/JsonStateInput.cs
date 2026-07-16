using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace BananaParty.WebSocketRelay
{
    public class JsonStateInput : IStateInput
    {
        private readonly string _jsonString;
        private int _position;
        private bool _hasStarted;
        private readonly Stack<bool> _arrayFirstItemScopes = new();
        private readonly Stack<int> _objectContentStarts = new();

        public JsonStateInput(string json)
        {
            _jsonString = json ?? "{}";
        }

        public void BeginArrayProperty(string name)
        {
            AdvanceToEntry(name);
            ReadArrayOpen();
            _arrayFirstItemScopes.Push(true);
        }

        public void BeginArrayElement()
        {
            SkipWhitespace();
            if (!_hasStarted)
            {
                ExpectCharacter('[');
                _hasStarted = true;
            }

            _arrayFirstItemScopes.Push(true);
        }

        public void EndArray()
        {
            SkipWhitespace();
            ExpectCharacter(']');

            if (_arrayFirstItemScopes.Count > 0)
                _arrayFirstItemScopes.Pop();
        }

        public void BeginObjectProperty(string name)
        {
            if (_objectContentStarts.Count == 0)
            {
                AdvanceToEntry(name);
                ReadObjectOpen();
                return;
            }

            int searchPosition = _objectContentStarts.Peek();
            _position = searchPosition;

            while (ReadNextPropertyKey(out string propertyKey))
            {
                if (string.Equals(propertyKey, name, StringComparison.OrdinalIgnoreCase))
                {
                    SkipWhitespace();
                    ExpectCharacter('{');
                    _objectContentStarts.Push(_position);
                    return;
                }

                SkipValue();
            }

            throw new KeyNotFoundException($"Network identity '{name}' was not found in JSON state.");
        }

        public void BeginObjectElement()
        {
            SkipWhitespace();
            if (!_hasStarted)
            {
                ExpectCharacter('{');
                _hasStarted = true;
                _objectContentStarts.Push(_position);
                return;
            }

            SkipArrayElementSeparator();
            ReadObjectOpen();
        }

        internal static IReadOnlyList<Guid> GetRootIdentityIds(string json)
        {
            JsonStateInput stateInput = new(json);
            stateInput.BeginObjectElement();

            List<Guid> identityIds = new();
            stateInput._position = stateInput._objectContentStarts.Peek();

            while (stateInput.ReadNextPropertyKey(out string propertyKey))
            {
                if (!Guid.TryParse(propertyKey, out Guid identityId))
                    throw new InvalidOperationException($"Invalid network identity key '{propertyKey}'.");

                identityIds.Add(identityId);
                stateInput.SkipValue();
            }

            return identityIds;
        }

        public void EndObject()
        {
            ReadObjectClose();

            if (_objectContentStarts.Count > 0)
                _objectContentStarts.Pop();
        }

        private bool ReadNextPropertyKey(out string propertyKey)
        {
            propertyKey = null;
            SkipWhitespace();

            if (_position >= _jsonString.Length || _jsonString[_position] == '}')
                return false;

            SkipItemSeparator();
            propertyKey = ReadQuotedString();

            if (propertyKey == null)
                throw new InvalidOperationException($"Expected quoted property key at position {_position}.");

            SkipColon();
            return true;
        }

        public string ReadString(string name)
        {
            AdvanceToEntry(name);

            if (_position < _jsonString.Length && _jsonString[_position] == '"')
                return ReadQuotedString();

            return ReadValueAsString();
        }

        public byte ReadByte(string name)
        {
            AdvanceToEntry(name);

            return ReadByteAtPosition();
        }

        public int ReadInt(string name)
        {
            AdvanceToEntry(name);

            return ReadIntAtPosition();
        }

        public long ReadLong(string name)
        {
            AdvanceToEntry(name);

            return ReadLongAtPosition();
        }

        public float ReadFloat(string name)
        {
            AdvanceToEntry(name);

            return ReadFloatAtPosition();
        }

        public double ReadDouble(string name)
        {
            AdvanceToEntry(name);

            return ReadDoubleAtPosition();
        }

        public bool ReadBool(string name)
        {
            AdvanceToEntry(name);

            return ReadBoolAtPosition();
        }

        public Vector2 ReadVector2(string name) =>
            ReadInlineObject(name, () =>
            {
                float x = ReadObjectComponentFloat("x");
                float y = ReadObjectComponentFloat("y");
                return new Vector2(x, y);
            });

        public Vector3 ReadVector3(string name) =>
            ReadInlineObject(name, () =>
            {
                float x = ReadObjectComponentFloat("x");
                float y = ReadObjectComponentFloat("y");
                float z = ReadObjectComponentFloat("z");
                return new Vector3(x, y, z);
            });

        public Vector2Int ReadVector2Int(string name) =>
            ReadInlineObject(name, () =>
            {
                int x = ReadObjectComponentInt("x");
                int y = ReadObjectComponentInt("y");
                return new Vector2Int(x, y);
            });

        public Vector3Int ReadVector3Int(string name) =>
            ReadInlineObject(name, () =>
            {
                int x = ReadObjectComponentInt("x");
                int y = ReadObjectComponentInt("y");
                int z = ReadObjectComponentInt("z");
                return new Vector3Int(x, y, z);
            });

        public Quaternion ReadQuaternion(string name) =>
            ReadInlineObject(name, () =>
            {
                float x = ReadObjectComponentFloat("x");
                float y = ReadObjectComponentFloat("y");
                float z = ReadObjectComponentFloat("z");
                float w = ReadObjectComponentFloat("w");
                return new Quaternion(x, y, z, w);
            });

        public Color ReadColor(string name) =>
            ReadInlineObject(name, () =>
            {
                float r = ReadObjectComponentFloat("r");
                float g = ReadObjectComponentFloat("g");
                float b = ReadObjectComponentFloat("b");
                float a = ReadObjectComponentFloat("a");
                return new Color(r, g, b, a);
            });

        public Guid ReadGuid(string name)
        {
            string value = ReadString(name);
            return Guid.TryParse(value, out Guid result) ? result : Guid.Empty;
        }

        private void AdvanceToEntry(string expectedName)
        {
            SkipItemSeparator();

            if (_position < _jsonString.Length && _jsonString[_position] == '{')
                _position++;

            SkipWhitespace();
            string entryName = ReadQuotedString();
            if (!string.IsNullOrEmpty(expectedName) && (entryName == null || entryName != expectedName))
                throw new KeyNotFoundException($"Expected field '{expectedName}' but found '{entryName ?? "null"}' in JSON state.");

            SkipColon();
        }

        private T ReadInlineObject<T>(string name, Func<T> readContent)
        {
            AdvanceToEntry(name);
            ReadObjectOpen();
            T result = readContent();
            ReadObjectClose();

            if (_objectContentStarts.Count > 0)
                _objectContentStarts.Pop();

            return result;
        }

        private void ReadObjectOpen()
        {
            SkipWhitespace();
            ExpectCharacter('{');
            _objectContentStarts.Push(_position);
        }

        private void ReadArrayOpen()
        {
            SkipWhitespace();
            ExpectCharacter('[');
        }

        private void ReadObjectClose()
        {
            SkipWhitespace();
            ExpectCharacter('}');
        }

        private void SkipArrayElementSeparator()
        {
            if (_arrayFirstItemScopes.Count == 0)
                return;

            bool isFirst = _arrayFirstItemScopes.Pop();
            if (!isFirst)
            {
                SkipWhitespace();
                ExpectCharacter(',');
                SkipWhitespace();
            }

            _arrayFirstItemScopes.Push(false);
        }

        private void ExpectCharacter(char expected)
        {
            SkipWhitespace();

            if (_position >= _jsonString.Length || _jsonString[_position] != expected)
                throw new InvalidOperationException($"Expected JSON '{expected}' at position {_position}.");

            _position++;
        }

        private float ReadObjectComponentFloat(string componentName)
        {
            SkipItemSeparator();

            string entryName = ReadQuotedString();
            if (entryName != componentName)
                throw new KeyNotFoundException($"Expected field '{componentName}' but found '{entryName ?? "null"}' in JSON object.");

            SkipColon();
            return ReadFloatAtPosition();
        }

        private int ReadObjectComponentInt(string componentName)
        {
            SkipItemSeparator();

            string entryName = ReadQuotedString();
            if (entryName != componentName)
                throw new KeyNotFoundException($"Expected field '{componentName}' but found '{entryName ?? "null"}' in JSON object.");

            SkipColon();
            return ReadIntAtPosition();
        }

        private void SkipValue()
        {
            SkipWhitespace();

            if (_position >= _jsonString.Length)
                return;

            char current = _jsonString[_position];
            if (current == '"')
            {
                ReadQuotedString();
                return;
            }

            if (current == '{')
            {
                ReadObjectOpen();
                SkipObjectContent();
                ReadObjectClose();

                if (_objectContentStarts.Count > 0)
                    _objectContentStarts.Pop();

                return;
            }

            if (current == '[')
            {
                ReadArrayOpen();
                SkipArrayContent();
                SkipWhitespace();
                ExpectCharacter(']');
                return;
            }

            ReadValueAsString();
        }

        private void SkipObjectContent()
        {
            SkipWhitespace();

            while (_position < _jsonString.Length && _jsonString[_position] != '}')
            {
                SkipItemSeparator();
                ReadQuotedString();
                SkipColon();
                SkipValue();
            }
        }

        private void SkipArrayContent()
        {
            while (true)
            {
                SkipWhitespace();

                if (_position >= _jsonString.Length || _jsonString[_position] == ']')
                    return;

                SkipItemSeparator();
                SkipValue();
            }
        }

        private void SkipItemSeparator()
        {
            SkipWhitespace();
            if (_position < _jsonString.Length && _jsonString[_position] == ',')
                _position++;
            SkipWhitespace();
        }

        private void SkipColon()
        {
            SkipWhitespace();
            if (_position < _jsonString.Length && _jsonString[_position] == ':')
                _position++;
            SkipWhitespace();
        }

        private string ReadQuotedString()
        {
            if (_position >= _jsonString.Length || _jsonString[_position] != '"')
                return null;

            _position++;
            int start = _position;
            while (_position < _jsonString.Length && _jsonString[_position] != '"')
                _position++;

            string value = _jsonString.Substring(start, _position - start);
            if (_position < _jsonString.Length)
                _position++;

            return value;
        }

        private string ReadValueAsString()
        {
            SkipWhitespace();

            if (_position < _jsonString.Length && _jsonString[_position] == '"')
                return ReadQuotedString();

            int valueStart = _position;
            while (_position < _jsonString.Length && _jsonString[_position] != ',' && _jsonString[_position] != '}' && _jsonString[_position] != ']' && !char.IsWhiteSpace(_jsonString[_position]))
                _position++;

            return _jsonString.Substring(valueStart, _position - valueStart).Trim();
        }

        private byte ReadByteAtPosition()
        {
            string value = ReadValueAsString();
            return byte.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte result) ? result : (byte)0;
        }

        private int ReadIntAtPosition()
        {
            string value = ReadValueAsString();
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) ? result : 0;
        }

        private long ReadLongAtPosition()
        {
            string value = ReadValueAsString();
            return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long result) ? result : 0L;
        }

        private float ReadFloatAtPosition()
        {
            string value = ReadValueAsString();
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result) ? result : 0f;
        }

        private double ReadDoubleAtPosition()
        {
            string value = ReadValueAsString();
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result) ? result : 0d;
        }

        private bool ReadBoolAtPosition()
        {
            string value = ReadValueAsString();
            if (!bool.TryParse(value, out bool result))
                return false;

            return result;
        }

        private void SkipWhitespace()
        {
            while (_position < _jsonString.Length && char.IsWhiteSpace(_jsonString[_position]))
                _position++;
        }
    }
}
