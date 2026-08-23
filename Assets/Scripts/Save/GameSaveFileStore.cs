using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Castlevania2D.Save
{
    public static class GameSaveFileStore
    {
        public const int SlotCount = 5;
        private const string DisplayFormat = "dd.MM.yyyy HH:mm";
        private const string FolderName = "Saves";

        public static bool TryWrite(int slotIndex, GameSaveData data)
        {
            if (!IsValidSlot(slotIndex) || data == null)
            {
                return false;
            }

            try
            {
                string folder = GetSaveFolder();
                Directory.CreateDirectory(folder);
                data.timestamp = DateTime.Now.ToString(DisplayFormat, CultureInfo.CurrentCulture);

                string path = GetSlotPath(slotIndex);
                string temporaryPath = path + ".tmp";
                File.WriteAllText(temporaryPath, JsonUtility.ToJson(data, true), Encoding.UTF8);

                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                File.Move(temporaryPath, path);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[GameSaveFileStore] Cannot write slot {slotIndex + 1}: {exception}");
                return false;
            }
        }

        public static bool TryRead(int slotIndex, out GameSaveData data)
        {
            data = null;
            if (!IsValidSlot(slotIndex))
            {
                return false;
            }

            string path = GetSlotPath(slotIndex);
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                data = JsonUtility.FromJson<GameSaveData>(File.ReadAllText(path, Encoding.UTF8));
                return data != null && data.version == 1;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[GameSaveFileStore] Cannot read slot {slotIndex + 1}: {exception}");
                data = null;
                return false;
            }
        }

        public static bool TryGetTimestamp(int slotIndex, out string timestamp)
        {
            if (TryRead(slotIndex, out GameSaveData data)
                && !string.IsNullOrWhiteSpace(data.timestamp))
            {
                timestamp = data.timestamp;
                return true;
            }

            timestamp = string.Empty;
            return false;
        }

        private static bool IsValidSlot(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < SlotCount;
        }

        private static string GetSlotPath(int slotIndex)
        {
            return Path.Combine(GetSaveFolder(), $"slot_{slotIndex + 1}.json");
        }

        private static string GetSaveFolder()
        {
            return Path.Combine(Application.persistentDataPath, FolderName);
        }
    }
}
