using System.Diagnostics;
using Verse;

namespace Blueprints {
    public static class Debug {
        private static string Decorate(string message) {
            return $"Blueprints :: {message}";
        }

        [Conditional("DEBUG")]
        public static void Message(string message) {
            Log.Message(Decorate(message));
        }
    }
}
