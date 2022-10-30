using RoR2;
using SkeleArtifact;
using EntityStates;
using R2API;
using System;
using Unity;
using UnityEngine;
using UnityEngine.Networking;

namespace SkeleArtifact {
    public class SkeleState : BaseState {
        private float stopwatch = 0f;
        private float delay = 1.5f;
        public override void OnEnter()
        {
            base.OnEnter();
        }

        public override void OnExit()
        {
            base.OnExit();
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            stopwatch += Time.fixedDeltaTime;
            if (stopwatch >= delay && base.fixedAge >= 5f) {
                stopwatch = 0f;
                if (NetworkServer.active) {
                    EffectManager.SpawnEffect(LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/OmniEffect/OmniExplosionVFXQuick"), new EffectData
                    {
                        origin = base.characterBody.corePosition,
                        scale = 8f,
                        rotation = Quaternion.identity
                    }, transmit: true);

                    BlastAttack blast = new()
                    {
                        attacker = base.gameObject,
                        baseDamage = 1,
                        radius = 8f,
                        inflictor = null,
                        falloffModel = BlastAttack.FalloffModel.None,
                        crit = true,
                        position = base.characterBody.corePosition,
                        procCoefficient = 0f,
                        damageColorIndex = DamageColorIndex.Item,
                        teamIndex = TeamIndex.Void,
                        damageType = DamageType.VoidDeath
                    };
                    blast.Fire();
                }
            }
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.Death;
        }
    }
}