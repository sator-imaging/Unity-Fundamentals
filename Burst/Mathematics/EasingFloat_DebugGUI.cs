using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

#nullable enable

namespace SatorImaging.UnityFundamentals
{
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasScaler))]
    [RequireComponent(typeof(GraphicRaycaster))]
    public sealed class EasingFloat_DebugGUI : MonoBehaviour
    {
        [Serializable]
        private sealed class DemoRow
        {
            public string FunctionName = string.Empty;
            public string VariantName = string.Empty;
            public Func<float, float>? Function;
            public RectTransform? BarFill;
            public RectTransform? Ball;
            public RectTransform? MotionTrackBackground;
        }

        private sealed class FunctionGroup
        {
            public string GroupName = string.Empty;
            public readonly List<MethodInfo> Methods = new List<MethodInfo>(3);
        }

        [Header("Playback")]
        [Range(0.1f, 5.0f)]
        [SerializeField] private float speed = 1.0f;
        [Min(0.05f)]
        [SerializeField] private float intervalSeconds = 0.1f;
        [SerializeField] private bool useDouble;

        [Header("Layout")]
        [SerializeField] private Vector2 panelSize = new Vector2(1440.0f, 1800.0f);
        [SerializeField] private float barMinHeight = 8.0f;
        [SerializeField] private float barMaxHeight = 52.0f;
        [SerializeField] private float ballTravel = 180.0f;
        [SerializeField] private float rowHeight = 58.0f;
        [SerializeField] private float cardHeight = 248.0f;
        [SerializeField] private int curveSampleCount = 64;
        [SerializeField] private int fontSize = 36;

        [Header("Colors")]
        [SerializeField] private Color backgroundColor = new Color(0.08f, 0.09f, 0.11f, 0.92f);
        [SerializeField] private Color trackColor = new Color(0.18f, 0.20f, 0.25f, 1.0f);
        [SerializeField] private Color barColor = new Color(0.28f, 0.78f, 0.52f, 1.0f);
        [SerializeField] private Color ballColor = new Color(0.97f, 0.69f, 0.22f, 1.0f);
        [SerializeField] private Color textColor = new Color(0.95f, 0.97f, 0.99f, 1.0f);

        private const string RootName = "__EasingFloatUGUIDemoRoot";
        private readonly List<DemoRow> _rows = new List<DemoRow>();
        private RectTransform? _demoRoot;
        private Font? _font;
        private Sprite? _curveDotSprite;
        private bool _didInitializeDefaults;
        private bool _builtWithDouble;
        private float _playhead;
        private float _holdTimer;
        private bool _isForward = true;

        private void Reset()
        {
            ConfigureRootComponents();
        }

        private void Awake()
        {
            ConfigureRootComponents();
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                ResetPlaybackState();
                RebuildUiIfNeeded();
            }
        }

        private void OnDisable()
        {
            CleanupGeneratedUi();
        }

        private void OnDestroy()
        {
            CleanupGeneratedUi();
        }

        private void OnValidate()
        {
            speed = Mathf.Max(0.1f, speed);
            intervalSeconds = Mathf.Max(0.05f, intervalSeconds);
            barMaxHeight = Mathf.Max(barMinHeight, barMaxHeight);
            rowHeight = Mathf.Max(32.0f, rowHeight);
            cardHeight = Mathf.Max(180.0f, cardHeight);
            ballTravel = Mathf.Max(64.0f, ballTravel);
            curveSampleCount = Mathf.Max(8, curveSampleCount);
            panelSize.x = Mathf.Max(980.0f, panelSize.x);
            panelSize.y = Mathf.Max(600.0f, panelSize.y);

            if (!Application.isPlaying)
            {
                ConfigureRootComponents();
            }
        }

        private void Update()
        {
            if (_builtWithDouble != useDouble)
            {
                ResetPlaybackState();
                RebuildUi();
                return;
            }

            if (_rows.Count == 0)
            {
                return;
            }

            float linear = AdvancePlayback(Time.deltaTime, Time.unscaledDeltaTime);

            for (int i = 0; i < _rows.Count; i++)
            {
                DemoRow row = _rows[i];
                if (row.Function == null || row.BarFill == null || row.Ball == null)
                {
                    continue;
                }

                float eased = row.Function.Invoke(linear);
                RectTransform? barTrack = row.BarFill.parent as RectTransform;
                float trackHeight = barTrack != null ? Mathf.Max(0.0f, barTrack.rect.height - 4.0f) : barMaxHeight;
                float barHeight = Mathf.LerpUnclamped(0.0f, trackHeight, eased);
                row.BarFill.sizeDelta = new Vector2(row.BarFill.sizeDelta.x, barHeight);

                RectTransform? motionTrack = row.Ball.parent as RectTransform;
                RectTransform? motionTrackBackground = row.MotionTrackBackground;
                float trackWidth = motionTrackBackground != null ? motionTrackBackground.rect.width : (motionTrack != null ? motionTrack.rect.width : ballTravel + 24.0f);
                float ballWidth = row.Ball.rect.width;
                float trackLeft = motionTrackBackground != null ? motionTrackBackground.offsetMin.x : 0.0f;
                float ballX = trackLeft + Mathf.LerpUnclamped(ballWidth * 0.5f, trackWidth - (ballWidth * 0.5f), eased);
                row.Ball.anchoredPosition = new Vector2(ballX, 0.0f);
            }
        }

        private void ConfigureRootComponents()
        {
            RectTransform rectTransform = GetComponent<RectTransform>();
            if (!_didInitializeDefaults)
            {
                CanvasScaler scaler = GetComponent<CanvasScaler>();
                Vector2 referenceResolution = scaler.referenceResolution;
                if (referenceResolution.x <= 0.0f || referenceResolution.y <= 0.0f)
                {
                    referenceResolution = new Vector2(1440.0f, 1800.0f);
                }

                rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.sizeDelta = referenceResolution;
                panelSize = referenceResolution;

                Canvas canvas = GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.pixelPerfect = false;

                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1440.0f, 1800.0f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                _didInitializeDefaults = true;
            }
        }

        private void RebuildUiIfNeeded()
        {
            _font ??= LoadBuiltinFont();

            bool needsBuild = false;
            Transform existing = transform.Find(RootName);
            if (existing == null)
            {
                needsBuild = true;
            }
            else if (existing.childCount != GetFunctionGroups().Count)
            {
                needsBuild = true;
            }
            else if (_builtWithDouble != useDouble)
            {
                needsBuild = true;
            }

            if (needsBuild)
            {
                RebuildUi();
            }
            else
            {
                CacheRows(existing as RectTransform);
            }
        }

        private void RebuildUi()
        {
            _font ??= LoadBuiltinFont();
            _curveDotSprite ??= LoadBuiltinSprite();
            CleanupGeneratedUi();

            _rows.Clear();
            _builtWithDouble = useDouble;

            _demoRoot = CreateUiObject<RectTransform>(RootName, transform);
            _demoRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _demoRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _demoRoot.pivot = new Vector2(0.5f, 0.5f);
            _demoRoot.sizeDelta = panelSize;

            Image background = _demoRoot.gameObject.AddComponent<Image>();
            background.color = backgroundColor;

            GridLayoutGroup layout = _demoRoot.gameObject.AddComponent<GridLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 20, 20);
            layout.spacing = new Vector2(14.0f, 14.0f);
            layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            layout.startAxis = GridLayoutGroup.Axis.Horizontal;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 2;
            layout.cellSize = new Vector2((panelSize.x - 54.0f) * 0.5f, cardHeight);

            ContentSizeFitter fitter = _demoRoot.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            List<FunctionGroup> groups = GetFunctionGroups();
            for (int i = 0; i < groups.Count; i++)
            {
                BuildGroup(groups[i]);
            }
        }

        private void CacheRows(RectTransform? root)
        {
            _rows.Clear();
            _demoRoot = root;
            if (root == null)
            {
                return;
            }

            List<FunctionGroup> groups = GetFunctionGroups();
            for (int i = 0; i < groups.Count; i++)
            {
                FunctionGroup group = groups[i];
                Transform cardTransform = root.Find(group.GroupName);
                if (cardTransform == null)
                {
                    RebuildUi();
                    return;
                }

                for (int j = 0; j < group.Methods.Count; j++)
                {
                    MethodInfo method = group.Methods[j];
                    string variant = GetVariantName(method.Name);
                    Transform rowTransform = cardTransform.Find("Variants/" + variant);
                    if (rowTransform == null)
                    {
                        RebuildUi();
                        return;
                    }

                    DemoRow row = new DemoRow
                    {
                        FunctionName = method.Name,
                        VariantName = variant,
                        Function = CreateFunction(method),
                        BarFill = rowTransform.Find("BarTrack/BarFill") as RectTransform,
                        Ball = rowTransform.Find("MotionTrack/Ball") as RectTransform,
                        MotionTrackBackground = rowTransform.Find("MotionTrack/TrackBg") as RectTransform
                    };

                    _rows.Add(row);
                }
            }
        }

        private void BuildGroup(FunctionGroup group)
        {
            RectTransform cardRoot = CreateUiObject<RectTransform>(group.GroupName, _demoRoot);
            LayoutElement cardLayout = cardRoot.gameObject.AddComponent<LayoutElement>();
            cardLayout.preferredWidth = (panelSize.x - 54.0f) * 0.5f;
            cardLayout.preferredHeight = cardHeight;

            Image cardBackground = cardRoot.gameObject.AddComponent<Image>();
            cardBackground.color = new Color(trackColor.r, trackColor.g, trackColor.b, 0.6f);

            Text header = CreateText("Header", cardRoot, group.GroupName, -1.0f);
            header.rectTransform.anchorMin = new Vector2(0.0f, 1.0f);
            header.rectTransform.anchorMax = new Vector2(1.0f, 1.0f);
            header.rectTransform.pivot = new Vector2(0.0f, 1.0f);
            header.rectTransform.offsetMin = new Vector2(12.0f, -34.0f);
            header.rectTransform.offsetMax = new Vector2(-12.0f, -12.0f);
            header.fontSize = fontSize + 2;
            header.alignment = TextAnchor.MiddleLeft;

            RectTransform variantsRoot = CreateUiObject<RectTransform>("Variants", cardRoot);
            variantsRoot.anchorMin = new Vector2(0.0f, 0.0f);
            variantsRoot.anchorMax = new Vector2(1.0f, 1.0f);
            variantsRoot.pivot = new Vector2(0.5f, 0.5f);
            variantsRoot.offsetMin = new Vector2(12.0f, 12.0f);
            variantsRoot.offsetMax = new Vector2(-12.0f, -42.0f);

            for (int i = 0; i < group.Methods.Count; i++)
            {
                BuildVariantRow(variantsRoot, group.Methods[i], i);
            }
        }

        private void BuildVariantRow(RectTransform parent, MethodInfo method, int index)
        {
            string variant = GetVariantName(method.Name);
            DemoRow row = new DemoRow
            {
                FunctionName = method.Name,
                VariantName = variant,
                Function = CreateFunction(method)
            };

            RectTransform rowRoot = CreateUiObject<RectTransform>(variant, parent);
            float top = 2.0f + (index * rowHeight);
            rowRoot.anchorMin = new Vector2(0.0f, 1.0f);
            rowRoot.anchorMax = new Vector2(1.0f, 1.0f);
            rowRoot.pivot = new Vector2(0.5f, 1.0f);
            rowRoot.offsetMin = new Vector2(0.0f, -(top + rowHeight));
            rowRoot.offsetMax = new Vector2(0.0f, -top);

            Image rowBackground = rowRoot.gameObject.AddComponent<Image>();
            rowBackground.color = new Color(backgroundColor.r, backgroundColor.g, backgroundColor.b, 0.45f);

            RectTransform variantPreview = CreateUiObject<RectTransform>("VariantPreview", rowRoot);
            SetHorizontalBox(variantPreview, 8.0f, 72.0f, 8.0f, rowHeight - 16.0f);
            if (row.Function != null)
            {
                BuildCurvePreview(variantPreview, row.Function);
            }

            RectTransform barTrack = CreateBarTrack(rowRoot);
            SetHorizontalBox(barTrack, 82.0f, 134.0f, 8.0f, rowHeight - 16.0f);
            row.BarFill = barTrack.Find("BarFill") as RectTransform;

            RectTransform motionTrack = CreateMotionTrack(rowRoot);
            SetAnchoredStretchBox(motionTrack, 144.0f, 8.0f, 8.0f, rowHeight - 16.0f);
            row.Ball = motionTrack.Find("Ball") as RectTransform;
            row.MotionTrackBackground = motionTrack.Find("TrackBg") as RectTransform;

            _rows.Add(row);
        }

        private RectTransform CreateBarTrack(Transform parent)
        {
            RectTransform barTrack = CreateUiObject<RectTransform>("BarTrack", parent);
            Image barTrackImage = barTrack.gameObject.AddComponent<Image>();
            barTrackImage.color = trackColor;

            RectTransform barFill = CreateUiObject<RectTransform>("BarFill", barTrack);
            barFill.anchorMin = new Vector2(0.5f, 0.0f);
            barFill.anchorMax = new Vector2(0.5f, 0.0f);
            barFill.pivot = new Vector2(0.5f, 0.0f);
            barFill.anchoredPosition = new Vector2(0.0f, 2.0f);
            barFill.sizeDelta = new Vector2(20.0f, 0.0f);
            Image barFillImage = barFill.gameObject.AddComponent<Image>();
            barFillImage.color = barColor;
            return barTrack;
        }

        private RectTransform CreateMotionTrack(Transform parent)
        {
            RectTransform motionTrack = CreateUiObject<RectTransform>("MotionTrack", parent);
            RectTransform trackBg = CreateUiObject<RectTransform>("TrackBg", motionTrack);
            trackBg.anchorMin = new Vector2(0.0f, 0.0f);
            trackBg.anchorMax = new Vector2(1.0f, 1.0f);
            trackBg.pivot = new Vector2(0.0f, 0.5f);
            trackBg.offsetMin = new Vector2(12.0f, 0.0f);
            trackBg.offsetMax = new Vector2(-12.0f, 0.0f);
            Image motionTrackImage = trackBg.gameObject.AddComponent<Image>();
            motionTrackImage.color = trackColor;

            RectTransform ball = CreateUiObject<RectTransform>("Ball", motionTrack);
            ball.anchorMin = new Vector2(0.0f, 0.5f);
            ball.anchorMax = new Vector2(0.0f, 0.5f);
            ball.pivot = new Vector2(0.5f, 0.5f);
            ball.sizeDelta = new Vector2(18.0f, 18.0f);
            ball.anchoredPosition = Vector2.zero;
            Image ballImage = ball.gameObject.AddComponent<Image>();
            ballImage.color = ballColor;
            return motionTrack;
        }

        private void BuildCurvePreview(RectTransform previewRoot, Func<float, float> function)
        {
            RectTransform plotRoot = CreateUiObject<RectTransform>("CurvePlot", previewRoot);
            plotRoot.anchorMin = new Vector2(0.0f, 0.0f);
            plotRoot.anchorMax = new Vector2(1.0f, 1.0f);
            plotRoot.pivot = new Vector2(0.5f, 0.5f);
            plotRoot.offsetMin = new Vector2(0.0f, 0.0f);
            plotRoot.offsetMax = new Vector2(0.0f, 0.0f);

            for (int i = 0; i < curveSampleCount; i++)
            {
                float x = curveSampleCount == 1 ? 0.0f : i / (float)(curveSampleCount - 1);
                float y = function.Invoke(x);

                RectTransform dot = CreateUiObject<RectTransform>("Knob" + i, plotRoot);
                dot.anchorMin = new Vector2(x, y);
                dot.anchorMax = new Vector2(x, y);
                dot.pivot = new Vector2(0.5f, 0.5f);
                dot.anchoredPosition = Vector2.zero;
                dot.sizeDelta = new Vector2(4.0f, 4.0f);

                Image dotImage = dot.gameObject.AddComponent<Image>();
                dotImage.color = textColor;
                if (_curveDotSprite != null)
                {
                    dotImage.sprite = _curveDotSprite;
                    dotImage.type = Image.Type.Simple;
                    dotImage.preserveAspect = true;
                }
            }
        }

        private Text CreateText(string name, Transform parent, string value, float width)
        {
            RectTransform textRect = CreateUiObject<RectTransform>(name, parent);
            if (width > 0.0f)
            {
                LayoutElement layout = textRect.gameObject.AddComponent<LayoutElement>();
                layout.preferredWidth = width;
                layout.minWidth = width;
            }

            Text text = textRect.gameObject.AddComponent<Text>();
            text.text = value;
            text.color = textColor;
            text.fontSize = fontSize;
            text.font = _font;
            text.fontStyle = FontStyle.Bold;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.alignment = TextAnchor.MiddleLeft;
            return text;
        }

        private static T CreateUiObject<T>(string name, Transform? parent) where T : Component
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            if (parent != null)
            {
                gameObject.transform.SetParent(parent, false);
            }

            return gameObject.GetComponent<T>();
        }

        private static Sprite? LoadBuiltinSprite()
        {
            return null; //Resources.GetBuiltinResource<Sprite>("Knob");
        }

        private void CleanupGeneratedUi()
        {
            Transform existing = transform.Find(RootName);
            if (existing != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(existing.gameObject);
                }
                else
                {
                    DestroyImmediate(existing.gameObject);
                }
            }

            _rows.Clear();
            _demoRoot = null;
        }

        private void ResetPlaybackState()
        {
            _playhead = 0.0f;
            _holdTimer = 0.0f;
            _isForward = true;
        }

        private float AdvancePlayback(float deltaTime, float unscaledDeltaTime)
        {
            if (_holdTimer > 0.0f)
            {
                _holdTimer -= unscaledDeltaTime;
                if (_holdTimer < 0.0f)
                {
                    _holdTimer = 0.0f;
                }

                return _playhead;
            }

            _playhead += (_isForward ? 1.0f : -1.0f) * (deltaTime * speed);

            if (_playhead >= 1.0f)
            {
                _playhead = 1.0f;
                _isForward = false;
                _holdTimer = intervalSeconds;
            }
            else if (_playhead <= 0.0f)
            {
                _playhead = 0.0f;
                _isForward = true;
                _holdTimer = intervalSeconds;
            }

            return _playhead;
        }

        private static void SetBox(RectTransform rectTransform, float x, float y, float width, float height)
        {
            rectTransform.anchorMin = new Vector2(0.0f, 1.0f);
            rectTransform.anchorMax = new Vector2(0.0f, 1.0f);
            rectTransform.pivot = new Vector2(0.0f, 1.0f);
            rectTransform.anchoredPosition = new Vector2(x, -y);
            rectTransform.sizeDelta = new Vector2(width, height);
        }

        private static void SetHorizontalBox(RectTransform rectTransform, float left, float right, float top, float height)
        {
            rectTransform.anchorMin = new Vector2(0.0f, 1.0f);
            rectTransform.anchorMax = new Vector2(0.0f, 1.0f);
            rectTransform.pivot = new Vector2(0.0f, 1.0f);
            rectTransform.anchoredPosition = new Vector2(left, -top);
            rectTransform.sizeDelta = new Vector2(right - left, height);
        }

        private static void SetAnchoredStretchBox(RectTransform rectTransform, float left, float top, float right, float height)
        {
            rectTransform.anchorMin = new Vector2(0.0f, 1.0f);
            rectTransform.anchorMax = new Vector2(1.0f, 1.0f);
            rectTransform.pivot = new Vector2(0.0f, 1.0f);
            rectTransform.offsetMin = new Vector2(left, -(top + height));
            rectTransform.offsetMax = new Vector2(-right, -top);
        }

        private Func<float, float>? CreateFunction(MethodInfo method)
        {
            if (!useDouble)
            {
                return (Func<float, float>)Delegate.CreateDelegate(typeof(Func<float, float>), method);
            }

            Func<double, double> function = (Func<double, double>)Delegate.CreateDelegate(typeof(Func<double, double>), method);
            return x => (float)function.Invoke(x);
        }

        private List<FunctionGroup> GetFunctionGroups()
        {
            List<MethodInfo> methods = new List<MethodInfo>();
            Type easingType = useDouble ? typeof(EasingDouble) : typeof(EasingFloat);
            Type scalarType = useDouble ? typeof(double) : typeof(float);
            MethodInfo[] candidates = easingType.GetMethods(BindingFlags.Public | BindingFlags.Static);
            for (int i = 0; i < candidates.Length; i++)
            {
                MethodInfo method = candidates[i];
                ParameterInfo[] parameters = method.GetParameters();
                if (method.ReturnType != scalarType || parameters.Length != 1 || parameters[0].ParameterType != scalarType)
                {
                    continue;
                }

                methods.Add(method);
            }

            methods.Sort(static (a, b) => CompareMethods(a.Name, b.Name));

            List<FunctionGroup> groups = new List<FunctionGroup>();
            FunctionGroup? current = null;
            for (int i = 0; i < methods.Count; i++)
            {
                MethodInfo method = methods[i];
                string groupName = GetGroupName(method.Name);
                if (current == null || current.GroupName != groupName)
                {
                    current = new FunctionGroup { GroupName = groupName };
                    groups.Add(current);
                }

                current.Methods.Add(method);
            }

            return groups;
        }

        private static int CompareMethods(string a, string b)
        {
            int groupCompare = GetGroupOrder(GetGroupName(a)).CompareTo(GetGroupOrder(GetGroupName(b)));
            if (groupCompare != 0)
            {
                return groupCompare;
            }

            return GetVariantOrder(a).CompareTo(GetVariantOrder(b));
        }

        private static string GetGroupName(string methodName)
        {
            if (methodName.EndsWith("InOut", StringComparison.Ordinal))
            {
                return methodName.Substring(0, methodName.Length - "InOut".Length);
            }

            if (methodName.EndsWith("Out", StringComparison.Ordinal))
            {
                return methodName.Substring(0, methodName.Length - "Out".Length);
            }

            if (methodName.EndsWith("In", StringComparison.Ordinal))
            {
                return methodName.Substring(0, methodName.Length - "In".Length);
            }

            return methodName;
        }

        private static string GetVariantName(string methodName)
        {
            if (methodName.EndsWith("InOut", StringComparison.Ordinal))
            {
                return "InOut";
            }

            if (methodName.EndsWith("Out", StringComparison.Ordinal))
            {
                return "Out";
            }

            if (methodName.EndsWith("In", StringComparison.Ordinal))
            {
                return "In";
            }

            return methodName;
        }

        private static int GetVariantOrder(string methodName)
        {
            string variant = GetVariantName(methodName);
            return variant switch
            {
                "In" => 0,
                "Out" => 1,
                "InOut" => 2,
                _ => 3,
            };
        }

        private static int GetGroupOrder(string groupName)
        {
            return groupName switch
            {
                "Sine" => 0,
                "Cubic" => 1,
                "Quad" => 2,
                "Quart" => 3,
                "Back" => 4,
                "Quint" => 5,
                "Bounce" => 6,
                "Expo" => 7,
                "Elastic" => 8,
                "Circ" => 9,
                _ => int.MaxValue,
            };
        }

        private static Font? LoadBuiltinFont()
        {
            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
    }
}
