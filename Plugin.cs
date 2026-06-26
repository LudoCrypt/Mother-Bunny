using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using DiskCardGame;
using HarmonyLib;
using InscryptionAPI.Card;
using InscryptionAPI.Helpers;
using InscryptionAPI.Nodes;
using InscryptionAPI.Triggers;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Transactions;
using UnityEngine;
using static MonoMod.InlineRT.MonoModRule;
using static UnityEngine.GraphicsBuffer;

namespace LudosCards
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("cyantist.inscryption.api", BepInDependency.DependencyFlags.HardDependency)]
    public class Plugin : BaseUnityPlugin
    {

        Harmony harmony = new Harmony(PluginGuid);

        private const string PluginGuid = "net_ludocrypt_ludoscards";
        private const string PluginName = "LudosCards";
        private const string PluginVersion = "1.0.0";
        private const string PluginPrefix = "ludoscards";

        public static List<Sprite> art_sprites;

        public static int randomSeed;

        public ConfigEntry<bool> configEnableNothing;

        public static ManualLogSource loggah;

        public abstract class ExtraDataClass<T, C> where T : class where C : class
        {
            private static readonly ConditionalWeakTable<T, C> weakData = new ConditionalWeakTable<T, C>();

            public static C GetData(T obj)
            {
                return weakData.GetOrCreateValue(obj);
            }
        }

        public class WeakCard : ExtraDataClass<PlayableCard, WeakCard>
        {
            public int lastHealthFromAttack = -1;
            public bool alreadyDrewFromDeath = false;
        }

        private void Awake()
        {
            Logger.LogInfo($"Loaded {PluginName}!");
            loggah = Logger;

            harmony.PatchAll(typeof(Plugin));

            AddSigils();
            AddCards();
        }

        private void AddSigils()
        {
            AbilityInfo bunnyMotherAbility = AbilityManager.New(
                PluginGuid + "_bunnymother",
                "Bunny Mother",
                "A card bearing this sigil will produce a bunny upon each point of damage taken.",
                typeof(BunnyMotherAbility),
                "bunny_mother_sigil.png"
            )
            .SetDefaultPart1Ability();

            BunnyMotherAbility.ability = bunnyMotherAbility.ability;

            Logger.LogInfo($"Added ludosbunny sigils!");
        }

        private void AddCards()
        {
            CardInfo bunnyMother = CardManager.New(
                modPrefix: PluginPrefix,
                "ludoscards_mother_bunny",
                "Bunny Mother",
                1,
                2,
                description: "a virile beast. in youth, quite vicious without a mother"
            )
            .SetCost(bloodCost: 1)
            .AddAbilities(BunnyMotherAbility.ability)
            .AddAppearances(CardAppearanceBehaviour.Appearance.RareCardBackground)
            .AddMetaCategories(CardMetaCategory.Rare)
            .SetPortrait("ludoscards_mother_bunny.png")
            .SetEmissivePortrait("ludoscards_mother_bunny_emission.png")
            .SetDefaultPart1Card();

            CardManager.Add(PluginPrefix, bunnyMother);

            CardInfo bunny = CardManager.New(
                modPrefix: PluginPrefix,
                "ludoscards_bunny",
                "Bunny",
                0,
                2
            )
            .SetCost(bloodCost: 0)
            .AddAbilities(Ability.Brittle)
            .AddSpecialAbilities(FeralBunnyAbility.FeralBunny)
            .SetPortrait("ludoscards_bunny.png");

            CardManager.Add(PluginPrefix, bunny);

            CardInfo feralBunny = CardManager.New(
                modPrefix: PluginPrefix,
                "ludoscards_feral_bunny",
                "Feral Bunny",
                2,
                2
            )
            .SetCost(bloodCost: 0)
            .AddAbilities(Ability.Brittle)
            .AddSpecialAbilities(BrittleBunnyAbility.BrittleBunny)
            .SetPortrait("ludoscards_bunny_feral.png");

            CardManager.Add(PluginPrefix, feralBunny);

            Logger.LogInfo($"Added ludosbunny cards!");

        }

        public class BunnyMotherAbility : AbilityBehaviour
        {
            public override Ability Ability
            {
                get
                {
                    return ability;
                }
            }

            public static Ability ability;


            public override bool RespondsToOtherCardDealtDamage(PlayableCard attacker, int amount, PlayableCard target)
            {
                return true;
            }

            public override bool RespondsToOtherCardDie(PlayableCard card, CardSlot deathSlot, bool fromCombat, PlayableCard killer)
            {
                return true;
            }

            public override IEnumerator OnOtherCardDie(PlayableCard card, CardSlot deathSlot, bool fromCombat, PlayableCard killer)
            {
                if (card.HasAbility(BunnyMotherAbility.ability))
                {
                    if (!WeakCard.GetData(card).alreadyDrewFromDeath)
                    {
                        if (WeakCard.GetData(card).lastHealthFromAttack == -1)
                        {
                            WeakCard.GetData(card).lastHealthFromAttack = card.MaxHealth;
                        }

                        WeakCard.GetData(card).alreadyDrewFromDeath = true;

                        // Im not sure if this is the best way to do this. If it dies in a single hit, draw as many cards as its current health.
                        return drawBunnies(WeakCard.GetData(card).lastHealthFromAttack, card);
                    }
                }
                return base.OnOtherCardDie(card, deathSlot, fromCombat, killer);
            }

            public override IEnumerator OnOtherCardDealtDamage(PlayableCard attacker, int amount, PlayableCard target)
            {
                if (target.HasAbility(BunnyMotherAbility.ability))
                {
                    if (!WeakCard.GetData(target).alreadyDrewFromDeath)
                    {
                        WeakCard.GetData(target).lastHealthFromAttack = target.Health;
                        return drawBunnies(amount, target);
                    }
                }

                return base.OnOtherCardDealtDamage(attacker, amount, target);
            }

            private IEnumerator drawBunnies(int amount, PlayableCard mother)
            {
                var initialCardInfo = CardLoader.GetCardByName("ludoscards_bunny");

                // make sure the card exists before creating copies of it
                if (initialCardInfo == null)
                {
                    yield break;
                }

                yield return base.PreSuccessfulTriggerSequence();

                if (Singleton<ViewManager>.Instance.CurrentView != View.Default)
                {
                    yield return new WaitForSeconds(0.2f);
                    Singleton<ViewManager>.Instance.SwitchToView(View.Default, false, false);
                    yield return new WaitForSeconds(0.2f);
                }

                for (int i = 0; i < amount; i++)
                {
                    var cardInfo = CardLoader.GetCardByName("ludoscards_bunny");
                    yield return Singleton<CardSpawner>.Instance.SpawnCardToHand(cardInfo, 0.25f);
                }

                yield return new WaitForSeconds(0.45f);
            }
        }

        public class FeralBunnyAbility : SpecialCardBehaviour
        {
            public static SpecialTriggeredAbility specialAbility;
            public readonly static SpecialTriggeredAbility FeralBunny = SpecialTriggeredAbilityManager.Add(PluginGuid, "Feral Bunny", typeof(FeralBunnyAbility)).Id;

            public override bool RespondsToOtherCardResolve(PlayableCard otherCard)
            {
                return true;
            }

            public override IEnumerator OnOtherCardResolve(PlayableCard otherCard)
            {
                bool isMotherInPlay = false;

                foreach (PlayableCard card in Singleton<BoardManager>.Instance.AllSlots.Select(x => x.Card).OfType<PlayableCard>().ToList())
                {
                    if (!card.Dead && card.HasAbility(BunnyMotherAbility.ability))
                    {
                        isMotherInPlay = true;
                    }
                }

                if (!isMotherInPlay)
                {
                    var cardInfo = CardLoader.GetCardByName("ludoscards_feral_bunny");
                    yield return base.PlayableCard.TransformIntoCard(cardInfo);
                }

                yield return base.OnOtherCardResolve(otherCard);
            }

            public override bool RespondsToOtherCardDealtDamage(PlayableCard attacker, int amount, PlayableCard target)
            {
                return true;
            }

            public override IEnumerator OnOtherCardDealtDamage(PlayableCard attacker, int amount, PlayableCard target)
            {
                bool isMotherInPlay = false;

                foreach (PlayableCard card in Singleton<BoardManager>.Instance.AllSlots.Select(x => x.Card).OfType<PlayableCard>().ToList())
                {
                    if (!card.Dead && card.HasAbility(BunnyMotherAbility.ability))
                    {
                        isMotherInPlay = true;
                    }
                }

                if (!isMotherInPlay)
                {
                    var cardInfo = CardLoader.GetCardByName("ludoscards_feral_bunny");
                    yield return base.PlayableCard.TransformIntoCard(cardInfo);

                    // for some reason, when a card attacks a steel trap, this method only fires when you attack the trap, not when the trap dies.
                    // so that means the 'attacker' is one of our cards, and the target is the steel trap
                    // but i suppose, if we attack a card, and that card ends up dead? that means we know what happened?
                    if (attacker.HasAbility(BunnyMotherAbility.ability))
                    {
                        if (attacker.Dead)
                        {
                            yield return Singleton<TurnManager>.Instance.CombatPhaseManager.SlotAttackSlot(base.PlayableCard.Slot, target.Slot, 0.35f);
                            // for some reason, manually calling an attack doesn't trigger the brittle sigil, so this is triggering it manually.
                            yield return base.PlayableCard.TriggerHandler.OnTrigger(Trigger.AttackEnded);
                        }
                    }
                }

                yield return base.OnOtherCardDealtDamage(attacker, amount, target);
            }
        }

        public class BrittleBunnyAbility : SpecialCardBehaviour
        {
            public static SpecialTriggeredAbility specialAbility;
            public readonly static SpecialTriggeredAbility BrittleBunny = SpecialTriggeredAbilityManager.Add(PluginGuid, "Brittle Bunny", typeof(BrittleBunnyAbility)).Id;


            public override bool RespondsToOtherCardResolve(PlayableCard otherCard)
            {
                return true;
            }

            public override IEnumerator OnOtherCardResolve(PlayableCard otherCard)
            {
                if (otherCard.HasAbility(BunnyMotherAbility.ability))
                {
                    var cardInfo = CardLoader.GetCardByName("ludoscards_bunny");
                    yield return base.PlayableCard.TransformIntoCard(cardInfo);
                }
                yield return base.OnOtherCardResolve(otherCard);
            }

        }

    }
}
