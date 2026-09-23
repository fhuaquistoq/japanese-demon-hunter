using System;
using UnityEngine;

namespace JapaneseDemonHunter.Monsters
{
    public interface IMonsterTarget
    {
        MonsterTargetType TargetType { get; }
        Transform Root { get; }
        Transform AttackPoint { get; }
        bool IsAvailable { get; }

        event Action<IMonsterTarget> AvailabilityChanged;

        bool TryReceiveHit(MonsterBase attacker);
    }
}
