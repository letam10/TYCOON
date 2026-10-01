using UnityEngine;
using UnityEngine.UI;

namespace TYCOON
{
    [DisallowMultipleComponent]
    public sealed class TycoonHud : MonoBehaviour
    {
        [SerializeField] private GameSession session;
        [SerializeField] private PlayerInteractor player;
        [SerializeField] private CheckoutStation checkout;
        private Text moneyText, carryText, capacityText, objectiveText, stageText, upgradeText, shopText, interactionText, feedbackText;
        private Font font;
        private Texture2D panelTexture;
        private Sprite panelSprite;
        private RectTransform canvasRoot;
        private float nextRefresh, moneyPulse;
        private int lastBalance = -1;
        private static readonly Color Cream = new Color(0.96f, 0.98f, 0.92f);
        private static readonly Color Mint = new Color(0.50f, 0.90f, 0.73f);
        private static readonly Color Gold = new Color(1f, 0.82f, 0.36f);
        private static readonly Color Panel = new Color(0.07f, 0.15f, 0.16f, 0.94f);

        public string MoneyText => moneyText != null ? moneyText.text : string.Empty;
        public string CarryText => carryText != null ? carryText.text : string.Empty;
        public string CapacityText => capacityText != null ? capacityText.text : string.Empty;
        public string ObjectiveText => objectiveText != null ? objectiveText.text : string.Empty;
        public string UpgradeText => upgradeText != null ? upgradeText.text : string.Empty;
        public string ShopText => shopText != null ? shopText.text : string.Empty;

        public void Configure(GameSession gameSession, PlayerInteractor actor, CheckoutStation station = null)
        {
            session = gameSession; player = actor; checkout = station;
            EnsureView();
            RefreshView();
        }

        private void Awake() => EnsureView();
        private void Update()
        {
            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + 0.1f;
                RefreshView();
            }
            if (moneyText != null && moneyPulse > 0f)
            {
                moneyPulse = Mathf.Max(0f, moneyPulse - Time.unscaledDeltaTime);
                float t = 1f - moneyPulse / 0.25f;
                moneyText.rectTransform.localScale = Vector3.one * (1f + Mathf.Sin(t * Mathf.PI) * 0.06f);
            }
        }

        public void RefreshView()
        {
            EnsureView();
            int balance = session != null ? session.Wallet.Balance : 0;
            if (lastBalance >= 0 && lastBalance != balance) moneyPulse = 0.25f;
            lastBalance = balance;
            Set(moneyText, "$" + balance.ToString("N0"));
            Set(stageText, session != null ? StageName(session.Stage).ToUpperInvariant() : "FARM");
            Set(objectiveText, session != null ? session.Objective : "Harvest goods and stock the shop to begin.");
            Set(carryText, CarryDescription());
            Set(capacityText, player != null ? player.Carry.Inventory.TotalCount + " / " + player.Carry.Inventory.Capacity + "  CAPACITY" : "0 / 0  CAPACITY");
            Set(shopText, checkout != null ? "Queue " + checkout.QueueCount + "   •   $" + checkout.PendingRevenue + " to collect" : "No checkout connected");
            PurchasePad nextPad = NextUpgrade();
            Set(upgradeText, nextPad != null ? nextPad.Definition.DisplayName + "\n$" + nextPad.CurrentCost.ToString("N0") : "All available upgrades owned");
            Set(interactionText, player != null && player.CurrentTarget != null ? "[E / A]  " + player.CurrentTarget.GetPrompt(player) : "WASD / stick to move  •  E / A to interact");
            Set(feedbackText, player != null ? player.StatusMessage : string.Empty);
            feedbackText.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(feedbackText.text));
        }

        private string CarryDescription()
        {
            if (player == null || player.Carry.Inventory.TotalCount == 0) return "Hands free";
            string result = string.Empty;
            int shown = 0;
            foreach (var stack in player.Carry.Inventory.Stacks)
            {
                if (shown++ == 2) { result += "  + more"; break; }
                if (result.Length > 0) result += ", ";
                result += stack.Quantity + " " + stack.Item.DisplayName;
            }
            return result;
        }

        private PurchasePad NextUpgrade()
        {
            if (session == null) return null;
            if (player != null && player.CurrentTarget is PurchasePad focused && focused.Definition != null) return focused;
            PurchasePad result = null;
            foreach (InteractionTarget target in InteractionTarget.ActiveTargets)
            {
                if (!(target is PurchasePad pad) || pad.Session != session || pad.Definition == null) continue;
                session.CanPurchase(pad.Definition, out PurchaseFailure reason);
                if (reason != PurchaseFailure.None && reason != PurchaseFailure.InsufficientMoney) continue;
                if (result == null || pad.CurrentCost < result.CurrentCost) result = pad;
            }
            return result;
        }

        private void EnsureView()
        {
            if (canvasRoot != null) return;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            CreateRoundedSprite();
            var canvasObject = new GameObject("TYCOON HUD Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            canvasRoot = canvasObject.GetComponent<RectTransform>();
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform walletPanel = CreatePanel("Wallet", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(280f, 94f));
            CreateText(walletPanel, "Wallet title", "TYCOON • WALLET", 13, Mint, new Vector2(20f, -12f), new Vector2(240f, 22f));
            moneyText = CreateText(walletPanel, "Money", "$0", 32, Gold, new Vector2(20f, -36f), new Vector2(240f, 48f));

            RectTransform carryPanel = CreatePanel("Carry", Vector2.one, Vector2.one, new Vector2(-24f, -24f), new Vector2(360f, 112f));
            CreateText(carryPanel, "Carry title", "CARRYING", 13, Mint, new Vector2(20f, -12f), new Vector2(320f, 20f));
            carryText = CreateText(carryPanel, "Items", "Hands free", 20, Cream, new Vector2(20f, -36f), new Vector2(320f, 34f));
            capacityText = CreateText(carryPanel, "Capacity", "", 13, Mint, new Vector2(20f, -77f), new Vector2(320f, 23f));

            RectTransform objectivePanel = CreatePanel("Objective", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -138f), new Vector2(360f, 168f));
            stageText = CreateText(objectivePanel, "Stage", "FARM", 13, Mint, new Vector2(20f, -14f), new Vector2(320f, 22f));
            CreateText(objectivePanel, "Objective title", "Your next step", 22, Cream, new Vector2(20f, -40f), new Vector2(320f, 32f));
            objectiveText = CreateText(objectivePanel, "Objective detail", "", 16, Cream, new Vector2(20f, -80f), new Vector2(320f, 78f));

            RectTransform shopPanel = CreatePanel("Shop status", Vector2.zero, Vector2.zero, new Vector2(24f, 24f), new Vector2(360f, 85f));
            CreateText(shopPanel, "Shop title", "SHOP STATUS", 13, Mint, new Vector2(20f, -12f), new Vector2(320f, 20f));
            shopText = CreateText(shopPanel, "Shop detail", "", 17, Cream, new Vector2(20f, -39f), new Vector2(320f, 34f));

            RectTransform upgradePanel = CreatePanel("Next upgrade", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(360f, 112f));
            CreateText(upgradePanel, "Upgrade title", "NEXT UPGRADE", 13, Mint, new Vector2(20f, -12f), new Vector2(320f, 20f));
            upgradeText = CreateText(upgradePanel, "Upgrade detail", "", 20, Gold, new Vector2(20f, -38f), new Vector2(320f, 64f));

            RectTransform promptPanel = CreatePanel("Interaction", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(680f, 64f));
            interactionText = CreateText(promptPanel, "Prompt", "", 19, Cream, new Vector2(20f, -15f), new Vector2(640f, 37f), TextAnchor.MiddleCenter);
            RectTransform feedbackPanel = CreatePanel("Feedback", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 102f), new Vector2(680f, 48f));
            feedbackText = CreateText(feedbackPanel, "Feedback detail", "", 17, Gold, new Vector2(16f, -8f), new Vector2(648f, 32f), TextAnchor.MiddleCenter);
        }

        private RectTransform CreatePanel(string name, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var panelObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = panelObject.GetComponent<RectTransform>();
            rect.SetParent(canvasRoot, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot; rect.anchoredPosition = position; rect.sizeDelta = size;
            Image background = panelObject.GetComponent<Image>();
            background.sprite = panelSprite; background.type = Image.Type.Sliced;
            background.color = Panel; background.raycastTarget = false;
            return rect;
        }

        private Text CreateText(RectTransform parent, string name, string content, int size, Color color,
            Vector2 position, Vector2 dimensions, TextAnchor alignment = TextAnchor.UpperLeft)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position; rect.sizeDelta = dimensions;
            Text label = textObject.GetComponent<Text>();
            label.font = font; label.fontSize = size; label.color = color;
            label.fontStyle = size >= 20 ? FontStyle.Bold : FontStyle.Normal;
            label.alignment = alignment; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
            label.text = content;
            return label;
        }

        private void CreateRoundedSprite()
        {
            const int size = 64;
            const float corner = 16f;
            panelTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "TYCOON Rounded Panel", filterMode = FilterMode.Bilinear };
            var pixels = new Color[size * size];
            // Bo góc bằng hình tròn trong texture nhỏ; không cần thêm file ảnh tạm.
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float centreX = Mathf.Clamp(x + 0.5f, corner, size - corner);
                    float centreY = Mathf.Clamp(y + 0.5f, corner, size - corner);
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(centreX, centreY));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(corner - distance));
                }
            panelTexture.SetPixels(pixels); panelTexture.Apply(false, true);
            panelSprite = Sprite.Create(panelTexture, new Rect(0f, 0f, size, size), Vector2.one * 0.5f, 100f, 0,
                SpriteMeshType.FullRect, new Vector4(corner, corner, corner, corner));
        }

        private void OnDestroy()
        {
            if (panelSprite != null) Destroy(panelSprite);
            if (panelTexture != null) Destroy(panelTexture);
        }
        private static void Set(Text label, string text) { if (label.text != text) label.text = text; }
        private static string StageName(BusinessStage value) => value == BusinessStage.FarmShop ? "Farm Shop" : value.ToString();
    }
}
