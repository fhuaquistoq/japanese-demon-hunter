using JapaneseDemonHunter.Monsters;
using JapaneseDemonHunter.Prototype;
using UnityEngine;

namespace JapaneseDemonHunter.Gameplay
{
    /// <summary>Spatial gameplay cues attached to the cart and to spawned enemies.</summary>
    [DisallowMultipleComponent]
    public sealed class GameSoundscape : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour speedSource;
        [SerializeField] private MonsterSpawner monsterSpawner;
        [SerializeField] private GiantZombieSpawner giantSpawner;
        [SerializeField] private AudioClip gallopClip;
        [SerializeField] private AudioClip horseBreathClip;
        [SerializeField] private AudioClip horseNeighClip;
        [SerializeField] private AudioClip zombieVoiceClip;
        [SerializeField] private AudioClip zombieDeathClip;
        [SerializeField] private AudioClip batWingsClip;
        [SerializeField] private AudioClip batDeathClip;
        [SerializeField] private AudioClip hitClip;
        [SerializeField] private AudioClip giantFootstepsClip;
        [SerializeField, Min(0.1f)] private float fullGallopSpeed = 4.5f;
        [SerializeField, Range(0f, 1f)] private float gallopVolume = 0.35f;
        [SerializeField, Min(1f)] private float horseBreathInterval = 19f;
        [SerializeField, Min(1f)] private float neighInterval = 45f;

        private ICartSpeedPenaltyReceiver speedReceiver;
        private AudioSource gallopSource;
        private AudioSource horseVoiceSource;
        private AudioSource giantSource;
        private float nextBreath;
        private float nextNeigh;

        private void Start()
        {
            speedReceiver = speedSource as ICartSpeedPenaltyReceiver;
            gallopSource = CreateSource(gameObject, gallopClip, true, 0f, 0.85f);
            horseVoiceSource = CreateSource(gameObject, null, false, 0.3f, 0.9f);
            if (gallopClip != null) gallopSource.Play();
            nextBreath = Time.time + horseBreathInterval;
            nextNeigh = Time.time + neighInterval;
            if (monsterSpawner != null) monsterSpawner.MonsterSpawned += OnMonsterSpawned;
            if (giantSpawner != null) giantSpawner.GiantSpawned += OnGiantSpawned;
        }

        private void OnDestroy()
        {
            if (monsterSpawner != null) monsterSpawner.MonsterSpawned -= OnMonsterSpawned;
            if (giantSpawner != null) giantSpawner.GiantSpawned -= OnGiantSpawned;
        }

        private void Update()
        {
            float speed = speedReceiver != null ? speedReceiver.EffectiveSpeed : 0f;
            float gait = Mathf.Clamp01(speed / Mathf.Max(0.1f, fullGallopSpeed));
            if (gallopSource != null)
            {
                gallopSource.volume = gallopVolume * gait;
                gallopSource.pitch = Mathf.Lerp(0.7f, 1.3f, gait);
            }

            if (horseVoiceSource != null && Time.time >= nextBreath)
            {
                if (horseBreathClip != null) horseVoiceSource.PlayOneShot(horseBreathClip, 0.55f);
                nextBreath = Time.time + horseBreathInterval;
            }

            if (horseVoiceSource != null && Time.time >= nextNeigh)
            {
                if (horseNeighClip != null) horseVoiceSource.PlayOneShot(horseNeighClip, 0.65f);
                nextNeigh = Time.time + neighInterval;
            }

            if (giantSource != null && giantSpawner != null && giantSpawner.SpawnedGiant != null)
            {
                giantSource.volume = giantSpawner.SpawnedGiant.IsVisible ? 0.55f : 0f;
            }
        }

        private void OnMonsterSpawned(MonsterBase monster)
        {
            if (monster == null) return;
            bool flying = monster.MovementType == MonsterMovementType.Flying;
            MonsterSoundEmitter emitter = monster.GetComponent<MonsterSoundEmitter>();
            if (emitter == null) emitter = monster.gameObject.AddComponent<MonsterSoundEmitter>();
            emitter.Configure(flying ? batWingsClip : zombieVoiceClip,
                flying ? batDeathClip : zombieDeathClip, hitClip);
        }

        private void OnGiantSpawned(GiantZombieController giant)
        {
            if (giant == null || giantFootstepsClip == null) return;
            giantSource = CreateSource(giant.gameObject, giantFootstepsClip, true, 0f, 1f);
            giantSource.maxDistance = 32f;
            giantSource.Play();
        }

        private static AudioSource CreateSource(GameObject owner, AudioClip clip, bool loop,
            float volume, float spatialBlend)
        {
            AudioSource source = owner.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.clip = clip;
            source.loop = loop;
            source.volume = volume;
            source.spatialBlend = spatialBlend;
            source.minDistance = 2f;
            source.maxDistance = 28f;
            source.rolloffMode = AudioRolloffMode.Linear;
            return source;
        }
    }

    [DisallowMultipleComponent]
    public sealed class MonsterSoundEmitter : MonoBehaviour
    {
        private AudioSource source;
        private MonsterDamageable damageable;
        private AudioClip deathClip;
        private AudioClip hitClip;

        public void Configure(AudioClip movementClip, AudioClip configuredDeathClip, AudioClip configuredHitClip)
        {
            deathClip = configuredDeathClip;
            hitClip = configuredHitClip;
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.minDistance = 1f;
            source.maxDistance = 24f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.volume = 0.32f;
            source.clip = movementClip;
            source.loop = true;
            if (movementClip != null) source.Play();
            damageable = GetComponent<MonsterDamageable>();
            if (damageable != null)
            {
                damageable.Damaged += OnDamaged;
                damageable.Killed += OnKilled;
            }
        }

        private void OnDestroy()
        {
            if (damageable != null)
            {
                damageable.Damaged -= OnDamaged;
                damageable.Killed -= OnKilled;
            }
        }

        private void OnDamaged(MonsterDamageable monster, float damage, Component attacker)
        {
            if (source != null && hitClip != null) source.PlayOneShot(hitClip, 0.5f);
        }

        private void OnKilled(MonsterDamageable monster)
        {
            if (source == null) return;
            source.Stop();
            source.loop = false;
            if (deathClip != null) source.PlayOneShot(deathClip, 0.8f);
        }
    }
}
