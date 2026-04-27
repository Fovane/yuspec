using UnityEngine;

namespace Yuspec.Unity.Samples.PureCSharpDungeon
{
    public sealed class PureCSharpDungeonInput : MonoBehaviour
    {
        [SerializeField] private PureCSharpDungeonGame game;

        public void Configure(PureCSharpDungeonGame configuredGame)
        {
            game = configuredGame;
        }

        private void Update()
        {
            if (game == null)
            {
                return;
            }

            var direction = new Vector2(ReadAxis(KeyCode.A, KeyCode.D), ReadAxis(KeyCode.S, KeyCode.W));
            game.MovePlayer(direction);

            if (Input.GetKeyDown(KeyCode.Space))
            {
                game.InteractWithNearestEntity();
            }
        }

        private static float ReadAxis(KeyCode negative, KeyCode positive)
        {
            var value = 0f;
            if (Input.GetKey(negative))
            {
                value -= 1f;
            }

            if (Input.GetKey(positive))
            {
                value += 1f;
            }

            return value;
        }
    }
}
