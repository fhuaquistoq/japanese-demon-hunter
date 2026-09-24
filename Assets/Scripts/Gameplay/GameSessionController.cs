using System.Collections.Generic;
using JapaneseDemonHunter.Monsters;
using Reins;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JapaneseDemonHunter.Gameplay
{
    /// <summary>
    /// Finite ride and in-world outcome. No HUD, timer, head-locked panel or forced camera motion.
    /// Added by LevelVictoryController at runtime so existing scenes keep their Inspector changes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameSessionController : MonoBehaviour
    {
        [Header("Partida")]
        [SerializeField, Min(1)] private int playerHitPoints = 5;
        [SerializeField, Min(0.5f)] private float restartHoldSeconds = 1.5f;

        [Header("Texto integrado en el mundo")]
        [SerializeField] private Vector3 resultLocalPosition = new Vector3(0f, 2.4f, -1.35f);
        [SerializeField, Min(0.01f)] private float resultTitleSize = 0.065f;
        [SerializeField, Min(0.01f)] private float restartInstructionSize = 0.025f;

        private LevelVictoryController victory;
        private DeathScreenEffect deathEffect;
        private PrototypeHunterMonsterTarget hunterTarget;
        private MonsterSpawner spawner;
        private GiantZombieSpawner giantSpawner;
        private CarriageMotor motor;
        private GameRunModel run;
        private GameObject resultRoot;
        private TextMesh resultText;
        private TextMesh restartText;
        private bool ended;
        private bool restartArmed;
        private bool restarting;
        private float restartHeldFor;

        public float ElapsedSeconds => run != null ? run.ElapsedSeconds : 0f;
        public int RemainingHits => run != null ? run.RemainingHits : playerHitPoints;
        public GameRunResult Result => run != null ? run.Result : GameRunResult.Playing;

        public void Configure(int hitPoints, float holdSeconds)
        {
            playerHitPoints = Mathf.Max(1, hitPoints);
            restartHoldSeconds = Mathf.Max(0.5f, holdSeconds);
        }

        private void Start()
        {
            victory = GetComponent<LevelVictoryController>();
            deathEffect = FindAnyObjectByType<DeathScreenEffect>();
            hunterTarget = FindAnyObjectByType<PrototypeHunterMonsterTarget>();
            spawner = FindAnyObjectByType<MonsterSpawner>();
            giantSpawner = FindAnyObjectByType<GiantZombieSpawner>();
            motor = FindAnyObjectByType<CarriageMotor>();
            run = new GameRunModel(playerHitPoints);

            if (victory != null) victory.LevelCompleted += HandleVictory;
            if (deathEffect != null) deathEffect.DefeatTriggered += HandleGiantDefeat;
            if (hunterTarget != null) hunterTarget.SimulatedHit += HandlePlayerHit;
            CreateWorldResult();
        }

        private void OnDestroy()
        {
            if (victory != null) victory.LevelCompleted -= HandleVictory;
            if (deathEffect != null) deathEffect.DefeatTriggered -= HandleGiantDefeat;
            if (hunterTarget != null) hunterTarget.SimulatedHit -= HandlePlayerHit;
        }

        private void Update()
        {
            if (run == null || restarting) return;
            if (ended)
            {
                UpdateRestart();
                return;
            }

            run.Tick(Time.unscaledDeltaTime);
        }

        private void HandleVictory()
        {
            if (run == null || !run.Win()) return;
            ended = true;
            if (deathEffect != null) deathEffect.ResetDefeat();
            ShowWorldResult("VICTORIA", new Color(1f, 0.76f, 0.35f));
        }

        private void HandleGiantDefeat()
        {
            if (run != null && run.Lose()) EndDefeat("El gigante alcanzó la carreta.");
        }

        private void HandlePlayerHit(MonsterBase attacker)
        {
            if (run == null || run.Result != GameRunResult.Playing) return;
            bool fatal = run.ReceiveHit();
            if (deathEffect != null)
            {
                deathEffect.SetDanger(1f - (float)run.RemainingHits / playerHitPoints);
                deathEffect.FlashDamage();
            }

            if (fatal) EndDefeat("Los monstruos te derrotaron.");
        }

        private void EndDefeat(string reason)
        {
            if (ended) return;
            ended = true;
            if (motor != null) motor.enabled = false;
            if (spawner != null)
            {
                spawner.enabled = false;
                foreach (MonsterBase monster in new List<MonsterBase>(spawner.ActiveMonsters))
                    if (monster != null) monster.Retire();
            }

            if (giantSpawner != null)
            {
                giantSpawner.enabled = false;
                if (giantSpawner.SpawnedGiant != null)
                    Destroy(giantSpawner.SpawnedGiant.gameObject);
            }

            if (deathEffect != null) deathEffect.TriggerDefeat();
            ShowWorldResult("GAME OVER", new Color(0.95f, 0.18f, 0.12f));
            Debug.Log("Japanese Demon Hunter: derrota. " + reason, this);
        }

        private void CreateWorldResult()
        {
            if (motor == null) return;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ??
                        Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font == null)
            {
                Debug.LogError("No se encontró una fuente para el resultado en el mundo.", this);
                return;
            }

            resultRoot = new GameObject("ResultadoEnCarreta");
            resultRoot.transform.SetParent(motor.transform, false);
            resultRoot.transform.localPosition = resultLocalPosition;
            resultText = CreateLetters("Resultado", font, Vector3.zero, resultTitleSize);
            restartText = CreateLetters("Reiniciar", font, new Vector3(0f, -0.34f, 0f),
                restartInstructionSize);
            restartText.text = "Suelta y toma ambas riendas para reiniciar";
            resultRoot.SetActive(false);
        }

        private TextMesh CreateLetters(string name, Font font, Vector3 localPosition, float size)
        {
            GameObject words = new GameObject(name, typeof(TextMesh), typeof(MeshRenderer));
            words.transform.SetParent(resultRoot.transform, false);
            words.transform.localPosition = localPosition;
            words.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            TextMesh letters = words.GetComponent<TextMesh>();
            letters.font = font;
            letters.fontSize = 64;
            letters.characterSize = size;
            letters.anchor = TextAnchor.MiddleCenter;
            letters.alignment = TextAlignment.Center;
            MeshRenderer renderer = words.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = font.material;
            renderer.sortingOrder = 100;
            return letters;
        }

        private void ShowWorldResult(string title, Color color)
        {
            if (resultText == null) return;
            resultText.text = title;
            resultText.color = color;
            restartText.color = color;
            resultRoot.SetActive(true);
        }

        private void UpdateRestart()
        {
            if (motor == null || motor.LeftRein == null || motor.RightRein == null) return;
            ReinHandle left = motor.LeftRein;
            ReinHandle right = motor.RightRein;
            left.UpdateGrip(Time.unscaledDeltaTime);
            right.UpdateGrip(Time.unscaledDeltaTime);
            if (!left.IsHeld && !right.IsHeld) restartArmed = true;
            if (!restartArmed || !left.IsHeldByExpectedHand || !right.IsHeldByExpectedHand)
            {
                restartHeldFor = 0f;
                return;
            }

            restartHeldFor += Time.unscaledDeltaTime;
            if (restartHeldFor < restartHoldSeconds) return;
            restarting = true;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0) SceneManager.LoadSceneAsync(scene.buildIndex);
            else SceneManager.LoadSceneAsync(scene.path);
        }
    }
}
