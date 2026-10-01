using System;
using System.Collections;
using UnityEngine;

namespace TYCOON
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class PurchasePad : InteractionTarget
    {
        [SerializeField] private UpgradeDefinition definition;
        [SerializeField] private GameSession session;
        [SerializeField] private GameObject[] unlockedObjects = Array.Empty<GameObject>();
        [SerializeField] private ProductionMachine upgradedMachine;
        [SerializeField] private ParticleSystem purchaseParticles;
        [SerializeField] private AudioSource feedbackAudio;
        [SerializeField] private AudioClip purchaseSound;
        [SerializeField] private AudioClip deniedSound;
        [SerializeField, Min(0.05f)] private float revealSeconds = 0.35f;
        private GameSession observedSession;
        private Coroutine revealRoutine;
        private Transform[] revealedTransforms;
        private Vector3[] revealScales;

        public event Action<PurchaseFailure> PurchaseAttempted;
        public UpgradeDefinition Definition => definition;
        public GameSession Session => session;
        public PurchaseFailure LastFailure { get; private set; }
        public string LastFeedback { get; private set; }
        public int CurrentCost => definition != null && definition.IsValid && session != null &&
            session.GetUpgradeLevel(definition.StableId) < definition.MaxLevel
            ? definition.CostAtLevel(session.GetUpgradeLevel(definition.StableId)) : 0;
        public override string Prompt => definition == null ? "Purchase" :
            session != null && session.GetUpgradeLevel(definition.StableId) >= definition.MaxLevel
                ? definition.DisplayName + " • Owned" : definition.DisplayName + " • $" + CurrentCost;

        public void Configure(UpgradeDefinition upgrade, GameSession gameSession, GameObject[] targets = null,
            ProductionMachine machine = null, float radius = 1.8f)
        {
            if (upgrade == null || gameSession == null) throw new ArgumentException("Purchase pad requires upgrade data and a game session.");
            definition = upgrade; session = gameSession;
            unlockedObjects = targets == null ? Array.Empty<GameObject>() : (GameObject[])targets.Clone();
            upgradedMachine = machine;
            ConfigureRadius(radius);
            GetComponent<BoxCollider>().isTrigger = true;
            ObserveSession();
            ApplyPurchasedState();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            GetComponent<BoxCollider>().isTrigger = true;
            ObserveSession();
            ApplyPurchasedState();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (observedSession != null) observedSession.Changed -= ApplyPurchasedState;
            observedSession = null;
            FinishReveal();
        }

        private void ObserveSession()
        {
            if (observedSession != null) observedSession.Changed -= ApplyPurchasedState;
            observedSession = session;
            if (observedSession != null && isActiveAndEnabled) observedSession.Changed += ApplyPurchasedState;
        }

        private void OnTriggerEnter(Collider other)
        {
            PlayerInteractor actor = other.GetComponentInParent<PlayerInteractor>();
            if (actor != null) TryPurchase(actor);
        }

        public override bool TryInteract(PlayerInteractor actor) => TryPurchase(actor);
        public bool TryPurchase(PlayerInteractor actor)
        {
            bool firstPurchase = definition != null && session != null && session.GetUpgradeLevel(definition.StableId) == 0;
            PurchaseFailure failure = PurchaseFailure.InvalidActor;
            bool succeeded = actor != null && session != null && actor.Session == session && session.TryPurchase(definition, out failure);
            LastFailure = failure;
            LastFeedback = succeeded ? "Unlocked " + definition.DisplayName + "!" : FailureMessage(failure);
            if (actor != null) actor.ShowFeedback(LastFeedback);
            if (feedbackAudio != null)
            {
                AudioClip clip = succeeded ? purchaseSound : deniedSound;
                if (clip != null) feedbackAudio.PlayOneShot(clip);
            }
            if (succeeded)
            {
                ApplyPurchasedState();
                if (purchaseParticles != null) purchaseParticles.Play();
                if (firstPurchase && Application.isPlaying) BeginReveal();
            }
            PurchaseAttempted?.Invoke(failure);
            return succeeded;
        }

        private void ApplyPurchasedState()
        {
            if (definition == null || session == null) return;
            int level = session.GetUpgradeLevel(definition.StableId);
            foreach (GameObject target in unlockedObjects)
                if (target != null && target != gameObject) target.SetActive(level > 0);
            if (upgradedMachine != null) upgradedMachine.ApplyLevel(level);
        }

        private void BeginReveal()
        {
            FinishReveal();
            revealedTransforms = new Transform[unlockedObjects.Length];
            revealScales = new Vector3[unlockedObjects.Length];
            for (int i = 0; i < unlockedObjects.Length; i++)
            {
                if (unlockedObjects[i] == null || unlockedObjects[i] == gameObject) continue;
                revealedTransforms[i] = unlockedObjects[i].transform;
                revealScales[i] = revealedTransforms[i].localScale;
                revealedTransforms[i].localScale = revealScales[i] * 0.02f;
            }
            revealRoutine = StartCoroutine(Reveal());
        }

        private IEnumerator Reveal()
        {
            float elapsed = 0f;
            while (elapsed < revealSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / revealSeconds);
                float scale = Mathf.Lerp(0.02f, 1f, 1f - Mathf.Pow(1f - t, 3f));
                for (int i = 0; i < revealedTransforms.Length; i++)
                    if (revealedTransforms[i] != null) revealedTransforms[i].localScale = revealScales[i] * scale;
                yield return null;
            }
            RestoreRevealScales();
            revealRoutine = null;
        }

        private void FinishReveal()
        {
            if (revealRoutine != null) StopCoroutine(revealRoutine);
            revealRoutine = null;
            RestoreRevealScales();
        }

        private void RestoreRevealScales()
        {
            if (revealedTransforms == null) return;
            for (int i = 0; i < revealedTransforms.Length; i++)
                if (revealedTransforms[i] != null) revealedTransforms[i].localScale = revealScales[i];
            revealedTransforms = null;
            revealScales = null;
        }

        private static string FailureMessage(PurchaseFailure failure)
        {
            switch (failure)
            {
                case PurchaseFailure.AlreadyPurchased: return "Already owned.";
                case PurchaseFailure.InsufficientMoney: return "Earn more money to buy this upgrade.";
                case PurchaseFailure.PrerequisiteMissing: return "Buy the required upgrade first.";
                case PurchaseFailure.StageLocked: return "Open the previous business area first.";
                default: return "This purchase is not available.";
            }
        }
    }
}
