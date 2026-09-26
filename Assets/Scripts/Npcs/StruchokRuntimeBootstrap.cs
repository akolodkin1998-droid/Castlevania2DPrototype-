using UnityEngine;
using UnityEngine.SceneManagement;

namespace Castlevania2D.Npcs
{
    public static class StruchokRuntimeBootstrap
    {
        private const string ObjectName = "Npc_Struchok";
        private const string PlayerObjectName = "Player_HeroKnight";
        private const float IdleFrameRate = 2f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void SpawnOrSyncInPrototype()
        {
            if (SceneManager.GetActiveScene().name != "Prototype")
            {
                return;
            }

            Sprite[] hideFrames = LoadNumbered("Npcs/Struchok/Struchok_Idle_", 6);
            Sprite[] followFrames = LoadNumbered("Npcs/Struchok/Struchok_Idle2_", 12);
            Sprite[] walkFrames = LoadNumbered("Npcs/Struchok/Struchok_Follow_", 15);
            Sprite[] vfxFrames = LoadNumbered("Npcs/Struchok/Struchok_Vfx_", 3);
            Sprite[] pickupFrames = LoadNumbered("Npcs/Struchok/Struchok_Pickup_", 9);
            Sprite[] jumpFrames = LoadNumbered("Npcs/Struchok/Struchok_Jump_", 9);
            if (hideFrames[0] == null)
            {
                return;
            }

            GameObject existing = GameObject.Find(ObjectName);
            if (existing != null)
            {
                Wire(existing, hideFrames, followFrames, walkFrames, vfxFrames, pickupFrames, jumpFrames);
                return;
            }

            var root = new GameObject(ObjectName);
            GameObject player = GameObject.Find(PlayerObjectName);
            root.transform.position = player != null
                ? player.transform.position + new Vector3(2.4f, 0f, 0f)
                : new Vector3(10.7f, -37f, 0f);

            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = hideFrames[0];
            renderer.sortingOrder = 3;
            Wire(root, hideFrames, followFrames, walkFrames, vfxFrames, pickupFrames, jumpFrames);
        }

        private static void Wire(
            GameObject root,
            Sprite[] hideFrames,
            Sprite[] followFrames,
            Sprite[] walkFrames,
            Sprite[] vfxFrames,
            Sprite[] pickupFrames,
            Sprite[] jumpFrames)
        {
            PairLoopIdleSprite2D pairLoop = root.GetComponent<PairLoopIdleSprite2D>();
            if (pairLoop != null)
            {
                pairLoop.enabled = false;
                pairLoop.SetFrameRate(IdleFrameRate);
            }

            if (root.GetComponent<CompanionLootCollector2D>() == null)
            {
                root.AddComponent<CompanionLootCollector2D>();
            }

            StruchokNpc2D npc = root.GetComponent<StruchokNpc2D>();
            if (npc == null)
            {
                npc = root.AddComponent<StruchokNpc2D>();
            }

            npc.AssignSets(hideFrames, followFrames, walkFrames, vfxFrames, pickupFrames, jumpFrames);
        }

        private static Sprite[] LoadNumbered(string prefix, int count)
        {
            var frames = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                frames[i] = Resources.Load<Sprite>(prefix + (i + 1).ToString("D3"));
            }

            return frames;
        }
    }
}
