using System;
using System.IO;

namespace SharpMemories
{
    public static class FileHelpers
    {
        public static string MakeSafeFilename(string name)
        {
            // Remove all invalid chars for a cleaner name
            var arr = name.ToCharArray();
            arr = Array.FindAll<char>(arr, (c => Array.IndexOf<char>(Path.GetInvalidFileNameChars(), c) < 0));
            return new string(arr);
        }
    }
}
