using UnityEngine;

namespace Castlevania2D.Npcs
{
    /// <summary>
    /// Remembers that the companion was called. The NPC itself is rebuilt after every scene load.
    /// </summary>
    public static class CompanionFollowSession
    {
        public const string ObjectName = "Npc_Struchok";

        public static bool Recruited { get; private set; }

        public static void MarkRecruited()
        {
            Recruited = true;
        }

        public static void Clear()
        {
            Recruited = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetDomain()
        {
            Recruited = false;
        }
    }
}
