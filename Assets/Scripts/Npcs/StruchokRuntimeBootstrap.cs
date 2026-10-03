using Castlevania2D.Hub;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Castlevania2D.Npcs
{
    public static class StruchokRuntimeBootstrap
    {
        private const string PlayerObjectName = "Player_HeroKnight";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void SpawnOrSync()
        {
            RebuildForActiveScene();
        }

        public static void RebuildForActiveScene()
        {
            DestroyLegacyPersistent();
            string sceneName = SceneManager.GetActiveScene().name;
            bool gameplayScene = sceneName == "Prototype" || sceneName == "Hub";
            if (!gameplayScene)
            {
                return;
            }

            if (sceneName == "Hub" && !CompanionFollowSession.Recruited)
            {
                return;
            }

            Sprite[] hideFrames;
            Sprite[] followFrames;
            Sprite[] walkFrames;
            Sprite[] vfxFrames;
            Sprite[] pickupFrames;
            Sprite[] jumpFrames;
            if (!TryLoadFrames(
                    out hideFrames,
                    out followFrames,
                    out walkFrames,
                    out vfxFrames,
                    out pickupFrames,
                    out jumpFrames))
            {
                return;
            }

            GameObject root = GameObject.Find(CompanionFollowSession.ObjectName);
            if (root == null)
            {
                root = new GameObject(CompanionFollowSession.ObjectName);
                GameObject player = GameObject.Find(PlayerObjectName);
                root.transform.position = player != null
                    ? player.transform.position + new Vector3(-1.8f, 0f, 0f)
                    : new Vector3(10.7f, -37f, 0f);
                SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = 3;
            }

            StruchokNpc2D npc = Wire(
                root,
                hideFrames,
                followFrames,
                walkFrames,
                vfxFrames,
                pickupFrames,
                jumpFrames);
            if (npc == null)
            {
                return;
            }

            if (CompanionFollowSession.Recruited)
            {
                npc.ResumeFollowingPlayer();
            }
        }

        private static void DestroyLegacyPersistent()
        {
            GameObject leftover = GameObject.Find("Npc_Struchok_Companion");
            if (leftover != null)
            {
                Object.Destroy(leftover);
            }
        }

        private static StruchokNpc2D Wire(
            GameObject root,
            Sprite[] hideFrames,
            Sprite[] followFrames,
            Sprite[] walkFrames,
            Sprite[] vfxFrames,
            Sprite[] pickupFrames,
            Sprite[] jumpFrames)
        {
            root.name = CompanionFollowSession.ObjectName;
            StripLegacyBehaviours(root);

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
            return npc;
        }

        private static void StripLegacyBehaviours(GameObject root)
        {
            PairLoopIdleSprite2D pairLoop = root.GetComponent<PairLoopIdleSprite2D>();
            if (pairLoop != null)
            {
                pairLoop.enabled = false;
                Object.Destroy(pairLoop);
            }

            NpcTalk2D talk = root.GetComponent<NpcTalk2D>();
            if (talk != null)
            {
                talk.enabled = false;
                talk.CloseTalk();
                Object.DestroyImmediate(talk);
            }

            Transform prompt = root.transform.Find("InteractionPrompt");
            if (prompt != null)
            {
                Object.DestroyImmediate(prompt.gameObject);
            }
        }

        private static bool TryLoadFrames(
            out Sprite[] hideFrames,
            out Sprite[] followFrames,
            out Sprite[] walkFrames,
            out Sprite[] vfxFrames,
            out Sprite[] pickupFrames,
            out Sprite[] jumpFrames)
        {
            hideFrames = LoadNumbered("Npcs/Struchok/Struchok_Idle_", 6);
            followFrames = LoadNumbered("Npcs/Struchok/Struchok_Idle2_", 12);
            walkFrames = LoadNumbered("Npcs/Struchok/Struchok_Follow_", 15);
            vfxFrames = LoadNumbered("Npcs/Struchok/Struchok_Vfx_", 3);
            pickupFrames = LoadNumbered("Npcs/Struchok/Struchok_Pickup_", 9);
            jumpFrames = LoadNumbered("Npcs/Struchok/Struchok_Jump_", 9);
            return hideFrames[0] != null;
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
