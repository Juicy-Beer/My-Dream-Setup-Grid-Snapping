using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using Furniture;
using UnityEngine;
using UnityEngine.Rendering;

namespace DreamSetupGridSnap
{
    [BepInPlugin("local.dreamsetup.gridsnap", "Dream Setup Grid Snap", "1.1.0")]
    public class GridSnapPlugin : BaseUnityPlugin
    {
        //unity units in metres so 1 inch is 0.0254
        private static readonly string[] SizeNames = { "1 cm", "1 inch", "5 cm", "10 cm", "25 cm", "50 cm", "1 m" };
        private static readonly float[] SizeValues = { 0.01f, 0.0254f, 0.05f, 0.10f, 0.25f, 0.50f, 1.00f };
        private static readonly string[] KeyLabels = { "Open menu", "Position snapping" };

        //settings (saves by BepInEx)
        private ConfigEntry<bool> _enabled;
        private ConfigEntry<float> _gridSize;
        private ConfigEntry<bool> _snapX, _snapY, _snapZ;
        private ConfigEntry<KeyCode>[] _keys = new ConfigEntry<KeyCode>[2];

        //state
        private bool _menuOpen;
        private Rect _window = new Rect(40, 40, 330, 10);
        private string _customText = "";
        private bool _typing;
        private int _rebind = -1;
        private string _toast = "";
        private float _toastUntil;

        private FurniturePiece[] _pieces = new FurniturePiece[0];
        private float _nextRefresh;
        private FieldInfo _hoverField;
        private PropertyInfo _hoverProp;

        //styles
        private bool _stylesBuilt;
        private const int R = 6;
        private GUIStyle _winStyle, _titleStyle, _sectionStyle, _labelStyle, _smallStyle, _toastStyle, _fieldStyle;
        private GUIStyle _btn, _btnSel, _btnOn, _btnOff, _btnDanger;

        private static readonly Color ColPanel = new Color(0.09f, 0.10f, 0.13f, 0.97f);
        private static readonly Color ColBtn = new Color(0.20f, 0.22f, 0.28f, 1f);
        private static readonly Color ColBtnHover = new Color(0.28f, 0.31f, 0.40f, 1f);
        private static readonly Color ColAccent = new Color(0.27f, 0.52f, 0.98f, 1f);
        private static readonly Color ColAccentHover = new Color(0.38f, 0.60f, 1f, 1f);
        private static readonly Color ColGreen = new Color(0.17f, 0.66f, 0.42f, 1f);
        private static readonly Color ColGreenHover = new Color(0.22f, 0.76f, 0.50f, 1f);
        private static readonly Color ColOff = new Color(0.30f, 0.30f, 0.35f, 1f);
        private static readonly Color ColRed = new Color(0.72f, 0.26f, 0.28f, 1f);
        private static readonly Color ColRedHover = new Color(0.86f, 0.33f, 0.35f, 1f);
        private static readonly Color ColField = new Color(0.05f, 0.06f, 0.08f, 1f);

        private void Awake()
        {
            _enabled = Config.Bind("Position", "Enabled", true, "Snap position to the grid");
            _gridSize = Config.Bind("Position", "GridSizeMetres", 0.10f, "Grid size in metres");
            _snapX = Config.Bind("Position", "SnapX", true, "Snap on the X axis");
            _snapY = Config.Bind("Position", "SnapY", false, "Snap on the Y (height) axis");
            _snapZ = Config.Bind("Position", "SnapZ", true, "Snap on the Z axis");
            _keys[0] = Config.Bind("Hotkeys", "OpenMenu", KeyCode.F8, "Open or close the menu");
            _keys[1] = Config.Bind("Hotkeys", "TogglePositionSnap", KeyCode.F7, "Turn position snapping on or off");

            const BindingFlags all = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            _hoverField = typeof(FurniturePiece).GetField("isHovering", all);
            _hoverProp = typeof(FurniturePiece).GetProperty("isHovering", all);
            if (_hoverField == null && _hoverProp == null)
                Logger.LogWarning("FurniturePiece.isHovering not found - snapping will not run.");

            //snap at alot points in the frame so the game cant move the piece after
            Camera.onPreCull += OnPreCullSnap;
            RenderPipelineManager.beginCameraRendering += OnSrpBegin;

            Logger.LogInfo("Grid Snap 1.1 loaded. Press " + _keys[0].Value + " for the menu.");
        }

        private void OnDestroy()
        {
            Camera.onPreCull -= OnPreCullSnap;
            RenderPipelineManager.beginCameraRendering -= OnSrpBegin;
        }

        //game state

        private bool IsHovering(FurniturePiece p)
        {
            try
            {
                if (_hoverField != null) return (bool)_hoverField.GetValue(p);
                if (_hoverProp != null) return (bool)_hoverProp.GetValue(p, null);
            }
            catch (Exception) { }
            return false;
        }

        private void Update()
        {
            HandleHotkeys();

            if (Time.unscaledTime >= _nextRefresh)
            {
                _pieces = UnityEngine.Object.FindObjectsOfType<FurniturePiece>();
                _nextRefresh = Time.unscaledTime + 0.25f;
            }
        }

        private void HandleHotkeys()
        {
            if (_rebind >= 0) return;

            if (Input.GetKeyDown(_keys[0].Value)) _menuOpen = !_menuOpen;
            if (_typing) return;

            if (Input.GetKeyDown(_keys[1].Value))
            {
                _enabled.Value = !_enabled.Value;
                Toast("Position snapping " + (_enabled.Value ? "ON" : "OFF"));
            }
        }

        private void Toast(string text)
        {
            _toast = text;
            _toastUntil = Time.unscaledTime + 1.5f;
        }

        //snapping

        private void LateUpdate() { SnapAll(); }
        private void OnPreCullSnap(Camera cam) { SnapAll(); }
        private void OnSrpBegin(ScriptableRenderContext ctx, Camera cam) { SnapAll(); }

        private void SnapAll()
        {
            if (!_enabled.Value || _gridSize.Value <= 0f) return;

            foreach (var piece in _pieces)
            {
                if (piece == null || !IsHovering(piece)) continue;

                Vector3 pos = piece.transform.position;
                float g = _gridSize.Value;
                if (_snapX.Value) pos.x = Mathf.Round(pos.x / g) * g;
                if (_snapY.Value) pos.y = Mathf.Round(pos.y / g) * g;
                if (_snapZ.Value) pos.z = Mathf.Round(pos.z / g) * g;
                piece.transform.position = pos;
            }
        }

        //menu

        private void OnGUI()
        {
            EnsureStyles();
            HandleRebindInput();
            _typing = GUI.GetNameOfFocusedControl() == "gridField";

            if (_menuOpen)
            {
                _window.height = 0f;
                _window = GUILayout.Window(918273, _window, DrawWindow, GUIContent.none, _winStyle, GUILayout.Width(330));
            }

            if (Time.unscaledTime < _toastUntil)
            {
                float w = 300f, h = 38f;
                GUI.Box(new Rect((Screen.width - w) / 2f, 24f, w, h), _toast, _toastStyle);
            }
        }

        private void HandleRebindInput()
        {
            if (_rebind < 0) return;
            Event ev = Event.current;
            if (ev == null || ev.type != EventType.KeyDown || ev.keyCode == KeyCode.None) return;

            if (ev.keyCode != KeyCode.Escape) _keys[_rebind].Value = ev.keyCode;
            _rebind = -1;
            ev.Use();
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("GRID SNAP", _titleStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("X", _btnDanger, GUILayout.Width(26), GUILayout.Height(22))) _menuOpen = false;
            GUILayout.EndHorizontal();
            GUILayout.Space(2);

            //position
            Section("POSITION");
            _enabled.Value = Switch("Snap position", _enabled.Value);
            GUILayout.Label("Grid size:  " + FormatSize(_gridSize.Value), _labelStyle);
            int clicked = PresetRow(SizeNames, SelectedIndex(SizeValues, _gridSize.Value, 0.00005f), 4);
            if (clicked >= 0) _gridSize.Value = SizeValues[clicked];

            GUILayout.BeginHorizontal();
            GUILayout.Label("Custom (cm)", _smallStyle, GUILayout.Width(80));
            GUI.SetNextControlName("gridField");
            _customText = GUILayout.TextField(_customText, _fieldStyle, GUILayout.Width(80), GUILayout.Height(24));
            if (GUILayout.Button("Set", _btn, GUILayout.Width(50), GUILayout.Height(24)))
            {
                float v;
                if (float.TryParse(_customText, out v) && v > 0f) _gridSize.Value = v / 100f;
                GUI.FocusControl("");
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Axes", _smallStyle, GUILayout.Width(80));
            _snapX.Value = AxisToggle("X", _snapX.Value);
            _snapY.Value = AxisToggle("Y (height)", _snapY.Value);
            _snapZ.Value = AxisToggle("Z", _snapZ.Value);
            GUILayout.EndHorizontal();

            //hotkeys
            Section("HOTKEYS  (click a key, then press a new one)");
            for (int i = 0; i < _keys.Length; i++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(KeyLabels[i], _labelStyle);
                GUILayout.FlexibleSpace();
                string text = _rebind == i ? "Press a key..." : _keys[i].Value.ToString();
                if (GUILayout.Button(text, _rebind == i ? _btnSel : _btn, GUILayout.Width(120), GUILayout.Height(24)))
                    _rebind = (_rebind == i) ? -1 : i;
                GUILayout.EndHorizontal();
            }
            if (_rebind >= 0) GUILayout.Label("Esc cancels", _smallStyle);

            GUILayout.Space(6);
            GUILayout.Label("Drag the top of this window to move it.", _smallStyle);

            GUI.DragWindow(new Rect(0, 0, 10000, 34));
        }

        private void Section(string text)
        {
            GUILayout.Space(8);
            GUILayout.Label(text, _sectionStyle);
        }

        private bool Switch(string label, bool value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _labelStyle);
            GUILayout.FlexibleSpace();
            bool clicked = GUILayout.Button(value ? "ON" : "OFF", value ? _btnOn : _btnOff,
                GUILayout.Width(60), GUILayout.Height(24));
            GUILayout.EndHorizontal();
            return clicked ? !value : value;
        }

        private bool AxisToggle(string label, bool value)
        {
            bool clicked = GUILayout.Button(label, value ? _btnSel : _btn, GUILayout.Height(24));
            return clicked ? !value : value;
        }

        //draws buttons in rows and shows the index clicked or -1
        private int PresetRow(string[] names, int selected, int perRow)
        {
            int result = -1;
            for (int i = 0; i < names.Length; i++)
            {
                if (i % perRow == 0) GUILayout.BeginHorizontal();
                if (GUILayout.Button(names[i], i == selected ? _btnSel : _btn, GUILayout.Height(26))) result = i;
                if (i % perRow == perRow - 1 || i == names.Length - 1) GUILayout.EndHorizontal();
            }
            return result;
        }

        private static int SelectedIndex(float[] values, float current, float tolerance)
        {
            for (int i = 0; i < values.Length; i++)
                if (Mathf.Abs(values[i] - current) < tolerance) return i;
            return -1;
        }

        private static string FormatSize(float metres)
        {
            string s = metres >= 1f ? metres.ToString("0.##") + " m" : (metres * 100f).ToString("0.##") + " cm";
            if (Mathf.Abs(metres - 0.0254f) < 0.00005f) s += "  (1 inch)";
            return s;
        }

        //styles

        private static Texture2D MakeRounded(Color fill)
        {
            int size = R * 2 + 4;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f;
                    float cx = Mathf.Clamp(fx, R, size - R);
                    float cy = Mathf.Clamp(fy, R, size - R);
                    float d = Mathf.Sqrt((fx - cx) * (fx - cx) + (fy - cy) * (fy - cy));
                    float a = Mathf.Clamp01(R - d + 0.5f);
                    px[y * size + x] = new Color(fill.r, fill.g, fill.b, fill.a * a);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        private GUIStyle MakeButton(Color normal, Color hover, Color textColor)
        {
            Texture2D n = MakeRounded(normal);
            Texture2D h = MakeRounded(hover);
            var s = new GUIStyle(GUI.skin.button);
            s.normal.background = n; s.focused.background = n;
            s.hover.background = h; s.active.background = h;
            s.onNormal.background = n; s.onFocused.background = n;
            s.onHover.background = h; s.onActive.background = h;
            s.normal.textColor = textColor; s.focused.textColor = textColor;
            s.hover.textColor = textColor; s.active.textColor = textColor;
            s.onNormal.textColor = textColor; s.onFocused.textColor = textColor;
            s.onHover.textColor = textColor; s.onActive.textColor = textColor;
            s.border = new RectOffset(R, R, R, R);
            s.margin = new RectOffset(2, 2, 2, 2);
            s.padding = new RectOffset(6, 6, 3, 3);
            s.alignment = TextAnchor.MiddleCenter;
            s.fontSize = 13;
            s.fontStyle = FontStyle.Normal;
            return s;
        }

        private void EnsureStyles()
        {
            if (_stylesBuilt) return;
            _stylesBuilt = true;

            Color white = new Color(0.94f, 0.95f, 0.98f, 1f);
            Color grey = new Color(0.62f, 0.66f, 0.74f, 1f);

            Texture2D panel = MakeRounded(ColPanel);
            _winStyle = new GUIStyle(GUI.skin.window);
            _winStyle.normal.background = panel; _winStyle.onNormal.background = panel;
            _winStyle.focused.background = panel; _winStyle.onFocused.background = panel;
            _winStyle.hover.background = panel; _winStyle.onHover.background = panel;
            _winStyle.active.background = panel; _winStyle.onActive.background = panel;
            _winStyle.border = new RectOffset(R, R, R, R);
            _winStyle.padding = new RectOffset(14, 14, 12, 14);

            _titleStyle = new GUIStyle(GUI.skin.label);
            _titleStyle.fontSize = 17; _titleStyle.fontStyle = FontStyle.Bold;
            _titleStyle.normal.textColor = white;

            _sectionStyle = new GUIStyle(GUI.skin.label);
            _sectionStyle.fontSize = 11; _sectionStyle.fontStyle = FontStyle.Bold;
            _sectionStyle.normal.textColor = ColAccentHover;

            _labelStyle = new GUIStyle(GUI.skin.label);
            _labelStyle.fontSize = 13; _labelStyle.normal.textColor = white;

            _smallStyle = new GUIStyle(GUI.skin.label);
            _smallStyle.fontSize = 11; _smallStyle.normal.textColor = grey;

            Texture2D field = MakeRounded(ColField);
            _fieldStyle = new GUIStyle(GUI.skin.textField);
            _fieldStyle.normal.background = field; _fieldStyle.focused.background = field;
            _fieldStyle.hover.background = field; _fieldStyle.active.background = field;
            _fieldStyle.normal.textColor = white; _fieldStyle.focused.textColor = white;
            _fieldStyle.hover.textColor = white; _fieldStyle.active.textColor = white;
            _fieldStyle.border = new RectOffset(R, R, R, R);
            _fieldStyle.padding = new RectOffset(6, 6, 3, 3);
            _fieldStyle.fontSize = 13;

            _toastStyle = new GUIStyle(GUI.skin.box);
            Texture2D toastBg = MakeRounded(new Color(0.09f, 0.10f, 0.13f, 0.92f));
            _toastStyle.normal.background = toastBg;
            _toastStyle.border = new RectOffset(R, R, R, R);
            _toastStyle.alignment = TextAnchor.MiddleCenter;
            _toastStyle.fontSize = 15; _toastStyle.fontStyle = FontStyle.Bold;
            _toastStyle.normal.textColor = white;

            _btn = MakeButton(ColBtn, ColBtnHover, white);
            _btnSel = MakeButton(ColAccent, ColAccentHover, Color.white);
            _btnOn = MakeButton(ColGreen, ColGreenHover, Color.white);
            _btnOff = MakeButton(ColOff, ColBtnHover, grey);
            _btnDanger = MakeButton(ColRed, ColRedHover, Color.white);
        }
    }
}
