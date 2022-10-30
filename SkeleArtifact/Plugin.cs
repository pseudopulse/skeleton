using BepInEx;
using R2API;
using RoR2;
using Unity;
using UnityEngine;
using R2API.Networking;
using R2API.Utils;
using System;
using System.Collections.Generic;
using System.Reflection;
using EntityStates;
using UnityEngine.AddressableAssets;
using RoR2.Skills;
using System.Linq;
using UnityEngine.Networking;

namespace SkeleArtifact {
    [BepInPlugin(ModGuid, ModName, ModVer)]
    [BepInDependency(R2API.R2API.PluginGUID, R2API.R2API.PluginVersion)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.EveryoneNeedSameModVersion)]
    [R2APISubmoduleDependency(nameof(DamageAPI), nameof(ItemAPI), nameof(LanguageAPI), nameof(EliteAPI), nameof(RecalculateStatsAPI), nameof(DirectorAPI), nameof(NetworkingAPI), nameof(PrefabAPI))]
    public class SkeleArtifact : BaseUnityPlugin {
        public AssetBundle bundle;
        public static ArtifactDef skele;
        public const string ModGuid = "com.pseudo.skeleartifact";
        public const string ModName = "SkeleArtifact";
        public const string ModVer = "1.0.0";
        public static CharacterSpawnCard card;

        public void Awake() {
            bundle = AssetBundle.LoadFromFile(Assembly.GetExecutingAssembly().Location.Replace("SkeleArtifact.dll", "skelebundle"));

            GameObject prefab = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Beetle/BeetleBody.prefab").WaitForCompletion().InstantiateClone("skeletonbody");
            GameObject prefabMaster = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Beetle/BeetleMaster.prefab").WaitForCompletion().InstantiateClone("skeletonmaster");
            CharacterBody body = prefab.GetComponent<CharacterBody>();
            body.baseMoveSpeed = 7;
            body.baseMaxHealth = 10000000;
            body.baseArmor = 10000000;
            body.baseDamage = 100000000;
            body.baseNameToken = "SKELETON_NAME";

            LanguageAPI.Add("SKELETON_NAME", "The Skeleton");

            card = ScriptableObject.CreateInstance<CharacterSpawnCard>();
            card.prefab = prefabMaster;
            card.directorCreditCost = 100000000;
            card.name = "cscSkele";
            card.hullSize = HullClassification.Golem;
            card.sendOverNetwork = true;
            card.noElites = true;

            GameObject model = bundle.LoadAsset<GameObject>("Assets/Cube.prefab").InstantiateClone("guh");
            SetupModel(prefab, model);
            BoxCollider box = model.GetComponentInChildren<BoxCollider>();
            SetupHurtbox(prefab, model, box, 0, true);

            model.GetComponent<HurtBoxGroup>().hurtBoxes = new HurtBox[] {
                box.GetComponentInChildren<HurtBox>()
            };

            model.GetComponent<HurtBoxGroup>().bullseyeCount = 1;
            model.GetComponent<HurtBoxGroup>().mainHurtBox = box.gameObject.GetComponent<HurtBox>();

            SerializableEntityStateType idle = new(typeof(SkeleState)); 

            EntityStateMachine guh = AddESM(prefab, "guh", idle); 

            SkillLocator sl = prefab.GetComponent<SkillLocator>();

            List<EntityStateMachine> machines = prefab.GetComponent<NetworkStateMachine>().stateMachines.Cast<EntityStateMachine>().ToList();
            machines.Add(guh);
            prefab.GetComponent<NetworkStateMachine>().stateMachines = machines.ToArray();

            SkillDef lockedDef = Addressables.LoadAssetAsync<SkillDef>("RoR2/Base/Captain/CaptainSkillDisconnected.asset").WaitForCompletion();

            ReplaceSkill(sl.primary, lockedDef);

            prefabMaster.GetComponent<CharacterMaster>().bodyPrefab = prefab;

            ContentAddition.AddBody(prefab);
            ContentAddition.AddMaster(prefabMaster);

            // artifact

            skele = ScriptableObject.CreateInstance<ArtifactDef>();
            skele.nameToken = "SKELE_ARTI_NAME";
            skele.descriptionToken = "SKELE_ARTI_DESC";
            skele.smallIconSelectedSprite = bundle.LoadAsset<Sprite>("Assets/ulley.png");
            skele.smallIconDeselectedSprite = bundle.LoadAsset<Sprite>("Assets/ulley.png");
            skele.cachedName = "SKELE_ARTI_NAME";
            
            LanguageAPI.Add("SKELE_ARTI_NAME", "Artifact of the Skeleton");
            LanguageAPI.Add("SKELE_ARTI_DESC", "A t-posing invincible skeleton chases you around and spews explosions that instantly any hit entity on contact");

            ContentAddition.AddArtifactDef(skele);

            On.RoR2.CharacterBody.OnDeathStart += reviveskele;
            On.RoR2.Stage.Start += skeletime;

        }

        public void skeletime(On.RoR2.Stage.orig_Start orig, Stage self) {
            orig(self);
            if (NetworkServer.active) {
                if (self) {
                    if (RunArtifactManager.instance && RunArtifactManager.instance.IsArtifactEnabled(skele)) {
                        Debug.Log("SkeleArtifact: Spawning Skeleton");
                        DirectorPlacementRule rule = new();
                        rule.placementMode = DirectorPlacementRule.PlacementMode.Random;
                        DirectorSpawnRequest request = new(card, rule, Run.instance.spawnRng);
                        request.teamIndexOverride = TeamIndex.Void;
                        DirectorCore.instance.TrySpawnObject(request);
                    }
                }
            }
        }

        public void reviveskele(On.RoR2.CharacterBody.orig_OnDeathStart orig, CharacterBody self) {
            if (NetworkServer.active) {
                if (self.baseNameToken == "SKELETON_NAME") {
                    Transform target = PlayerCharacterMasterController.instances[UnityEngine.Random.RandomRange(0, PlayerCharacterMasterController.instances.Count)].master.GetBody().transform;
                    if (RunArtifactManager.instance && RunArtifactManager.instance.IsArtifactEnabled(skele) && target) {
                        Debug.Log("SkeleArtifact: Reviving Skeleton");
                        DirectorPlacementRule rule = new();
                        rule.placementMode = DirectorPlacementRule.PlacementMode.Random;
                        DirectorSpawnRequest request = new(card, rule, Run.instance.spawnRng);
                        request.teamIndexOverride = TeamIndex.Void;
                        DirectorCore.instance.TrySpawnObject(request);
                    }
                }
            }
            orig(self);
        }

        public EntityStateMachine AddESM(GameObject prefab, string name, SerializableEntityStateType initial) {
            EntityStateMachine esm = prefab.AddComponent<EntityStateMachine>();
            esm.customName = name;
            esm.name = name;
            esm.initialStateType = initial;
            esm.mainStateType = initial;

            return esm;
        }

        public void ReplaceSkill(GenericSkill slot, SkillDef replaceWith, string familyName = "temp")
        {
            SkillFamily family = ScriptableObject.CreateInstance<SkillFamily>();
            ((ScriptableObject)family).name = familyName;
            // family.variants = new SkillFamily.Variant[1];
            slot._skillFamily = family;
            slot._skillFamily.variants = new SkillFamily.Variant[1];

            slot._skillFamily.variants[0] = new SkillFamily.Variant
            {
                skillDef = replaceWith
            };
        }

        public void SetupModel(GameObject prefab, GameObject model, float turnSpeed = 1200f)
        {
            DestroyModelLeftovers(prefab);
            foreach (HurtBoxGroup hurtboxes in prefab.GetComponentsInChildren<HurtBoxGroup>())
            {
                GameObject.Destroy(hurtboxes);
            }
            foreach (HurtBox hurtbox in prefab.GetComponentsInChildren<HurtBox>())
            {
                GameObject.Destroy(hurtbox);
            }

            GameObject modelbase = new("Model Base");
            modelbase.transform.SetParent(prefab.transform);
            modelbase.transform.SetPositionAndRotation(prefab.transform.position, prefab.transform.rotation);

            model.transform.SetParent(modelbase.transform);
            model.transform.SetPositionAndRotation(modelbase.transform.position, modelbase.transform.rotation);

            ModelLocator modelLocator = prefab.GetComponentInChildren<ModelLocator>();
            modelLocator.modelTransform = model.transform;
            modelLocator.modelBaseTransform = modelbase.transform;
            modelLocator.dontReleaseModelOnDeath = false;
            modelLocator.dontDetatchFromParent = false;
            modelLocator.noCorpse = true;
            modelLocator.preserveModel = false;
            modelLocator.autoUpdateModelTransform = true;

            CharacterDirection characterDirection = prefab.GetComponent<CharacterDirection>();
            if (characterDirection)
            {
                characterDirection.targetTransform = modelbase.transform;
                characterDirection.turnSpeed = turnSpeed;
            }

            CharacterDeathBehavior characterDeathBehavior = prefab.GetComponent<CharacterDeathBehavior>();
            characterDeathBehavior.deathStateMachine = prefab.GetComponent<EntityStateMachine>();
            characterDeathBehavior.deathState = new SerializableEntityStateType(typeof(GenericCharacterDeath));

            GameObject.Destroy(prefab.GetComponentInChildren<Animator>());

            model.AddComponent<HurtBoxGroup>();
        }

        public void SetupHurtbox(GameObject prefab, GameObject model, Collider collidier, short index, bool weakPoint = false, HurtBox.DamageModifier damageModifier = HurtBox.DamageModifier.Normal)
        {
            HurtBoxGroup hurtBoxGroup = model.GetComponent<HurtBoxGroup>();

            HurtBox componentInChildren = collidier.gameObject.AddComponent<HurtBox>();
            componentInChildren.gameObject.layer = LayerIndex.entityPrecise.intVal;
            componentInChildren.healthComponent = prefab.GetComponent<HealthComponent>();
            componentInChildren.isBullseye = weakPoint;
            componentInChildren.damageModifier = damageModifier;
            componentInChildren.hurtBoxGroup = hurtBoxGroup;
            componentInChildren.indexInGroup = index;
        }

        public void DestroyModelLeftovers(GameObject prefab)
        {
            GameObject.Destroy(prefab.GetComponentInChildren<ModelLocator>().modelBaseTransform.gameObject);
        }
    }
}