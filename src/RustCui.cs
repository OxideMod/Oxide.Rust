extern alias References;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Oxide.Core;
using Oxide.Pooling;
using References::Newtonsoft.Json;
using References::Newtonsoft.Json.Converters;
using References::Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Oxide.Game.Rust.Cui
{
    public sealed class JsonArrayPool<T> : IArrayPool<T>
    {
        public static readonly JsonArrayPool<T> Shared = new JsonArrayPool<T>();
        private static readonly IArrayPoolProvider<T> Provider = GetOrCreateProvider();

        private static IArrayPoolProvider<T> GetOrCreateProvider()
        {
            if (Interface.Oxide.PoolFactory.IsHandledType<T[]>())
            {
                return Interface.Oxide.PoolFactory.GetArrayProvider<T>();
            }

            Interface.Oxide.PoolFactory.RegisterProvider<BaseArrayPoolProvider<T>>(out var provider, 1000, 16384);
            return provider;
        }

        public T[] Rent(int minimumLength) => Provider.Take(minimumLength);
        public void Return(T[] array) => Provider.Return(array);
    }

    public static class CuiHelper
    {
        private class JsonWriterResources
        {
            public readonly StringBuilder StringBuilder = new StringBuilder(64 * 1024);
            public readonly StringWriter StringWriter;
            public readonly JsonTextWriter JsonWriter;
            public readonly JsonSerializer Serializer;

            public JsonWriterResources()
            {
                StringWriter = new StringWriter(StringBuilder, CultureInfo.InvariantCulture);
                JsonWriter = new JsonTextWriter(StringWriter)
                {
                    ArrayPool = JsonArrayPool<char>.Shared,
                    CloseOutput = false
                };
                Serializer = JsonSerializer.Create(Settings);
            }

            public void Reset(bool format = false)
            {
                StringBuilder.Clear();
                JsonWriter.Formatting = format ? Formatting.Indented : Formatting.None;
            }
        }

        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            DefaultValueHandling = DefaultValueHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            FloatFormatHandling = FloatFormatHandling.Symbol,
            StringEscapeHandling = StringEscapeHandling.Default
        };

        private static readonly ThreadLocal<JsonWriterResources> SharedWriterResources =
            new ThreadLocal<JsonWriterResources>(() => new JsonWriterResources());

        public static string ToJson(IReadOnlyList<CuiElement> elements, bool format = false)
        {
            var resources = SharedWriterResources.Value;
            resources.Reset(format);

            resources.Serializer.Serialize(resources.JsonWriter, elements);
            resources.JsonWriter.Flush();

            return resources.StringBuilder.ToString().Replace("\\n", "\n");
        }

        public static List<CuiElement> FromJson(string json) => JsonConvert.DeserializeObject<List<CuiElement>>(json);

        public static string GetGuid() => Guid.NewGuid().ToString("N");

        public static bool AddUi(BasePlayer player, List<CuiElement> elements)
        {
            if (player?.net == null)
            {
                return false;
            }

            return AddUi(player, ToJson(elements));
        }

        public static bool AddUi(BasePlayer player, string json)
        {
            if (player?.net != null && Interface.CallHook("CanUseUI", player, json) == null)
            {
                CommunityEntity.ServerInstance.ClientRPC(RpcTarget.Player("AddUI", player.net.connection), json);
                return true;
            }

            return false;
        }

        public static bool DestroyUi(BasePlayer player, string elem)
        {
            if (player?.net != null)
            {
                Interface.CallHook("OnDestroyUI", player, elem);
                CommunityEntity.ServerInstance.ClientRPC(RpcTarget.Player("DestroyUI", player.net.connection), elem);
                return true;
            }

            return false;
        }

        public static void SetColor(this ICuiColor elem, Color color)
        {
            elem.Color = $"{color.r} {color.g} {color.b} {color.a}";
        }

        public static Color GetColor(this ICuiColor elem) => ColorEx.Parse(elem.Color);
    }

    public class CuiElementContainer : List<CuiElement>
    {
        public string Add(CuiButton button, string parent = "Hud", string name = null, string destroyUi = null)
        {
            if (string.IsNullOrEmpty(name))
            {
                name = CuiHelper.GetGuid();
            }

            Add(new CuiElement
            {
                Name = name,
                Parent = parent,
                FadeOut = button.FadeOut,
                DestroyUi = destroyUi,
                Components =
                {
                    button.Button,
                    button.RectTransform
                }
            });
            if (!string.IsNullOrEmpty(button.Text.Text))
            {
                Add(new CuiElement
                {
                    Parent = name,
                    FadeOut = button.FadeOut,
                    Components =
                    {
                        button.Text,
                        new CuiRectTransformComponent()
                    }
                });
            }
            return name;
        }

        public string Add(CuiLabel label, string parent = "Hud", string name = null, string destroyUi = null)
        {
            if (string.IsNullOrEmpty(name))
            {
                name = CuiHelper.GetGuid();
            }

            Add(new CuiElement
            {
                Name = name,
                Parent = parent,
                FadeOut = label.FadeOut,
                DestroyUi = destroyUi,
                Components =
                {
                    label.Text,
                    label.RectTransform
                }
            });
            return name;
        }

        public string Add(CuiPanel panel, string parent = "Hud", string name = null, string destroyUi = null)
        {
            if (string.IsNullOrEmpty(name))
            {
                name = CuiHelper.GetGuid();
            }

            CuiElement element = new CuiElement
            {
                Name = name,
                Parent = parent,
                FadeOut = panel.FadeOut,
                DestroyUi = destroyUi
            };

            if (panel.Image != null)
            {
                element.Components.Add(panel.Image);
            }

            if (panel.RawImage != null)
            {
                element.Components.Add(panel.RawImage);
            }

            element.Components.Add(panel.RectTransform);

            if (panel.CursorEnabled)
            {
                element.Components.Add(new CuiNeedsCursorComponent());
            }

            if (panel.KeyboardEnabled)
            {
                element.Components.Add(new CuiNeedsKeyboardComponent());
            }

            Add(element);
            return name;
        }

        public string ToJson() => ToString();

        public override string ToString() => CuiHelper.ToJson(this);
    }

    public class CuiButton
    {
        public CuiButtonComponent Button { get; } = new CuiButtonComponent();
        public CuiRectTransformComponent RectTransform { get; } = new CuiRectTransformComponent();
        public CuiTextComponent Text { get; } = new CuiTextComponent();
        public float FadeOut { get; set; }
    }

    public class CuiPanel
    {
        public CuiImageComponent Image { get; set; } = new CuiImageComponent();
        public CuiRawImageComponent RawImage { get; set; }
        public CuiRectTransformComponent RectTransform { get; } = new CuiRectTransformComponent();
        public bool CursorEnabled { get; set; }
        public bool KeyboardEnabled { get; set; }
        public float FadeOut { get; set; }
    }

    public class CuiLabel
    {
        public CuiTextComponent Text { get; } = new CuiTextComponent();
        public CuiRectTransformComponent RectTransform { get; } = new CuiRectTransformComponent();
        public float FadeOut { get; set; }
    }

    public class CuiElement
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("parent")]
        public string Parent { get; set; }

        [JsonProperty("destroyUi")]
        public string DestroyUi { get; set; }

        [JsonProperty("components")]
        public List<ICuiComponent> Components { get; } = new List<ICuiComponent>();

        [JsonProperty("fadeOut")]
        public float FadeOut { get; set; }

        [JsonProperty("update")]
        public bool Update { get; set; }

        [JsonProperty("activeSelf")]
        public bool? ActiveSelf { get; set; }
    }

    [JsonConverter(typeof(ComponentConverter))]
    public interface ICuiComponent
    {
        [JsonProperty("type")]
        string Type { get; }
    }

    public interface ICuiGraphic
    {
        [JsonProperty("fadeIn")]
        float FadeIn { get; set; }

        [JsonProperty("placeholderParentId")]
        string PlaceholderParentId { get; set; }
    }

    public interface ICuiColor
    {
        [JsonProperty("color")]
        string Color { get; set; }
    }

    public interface ICuiEnableable
    {
        [JsonProperty("enabled")]
        bool? Enabled { get; set; }
    }

    public class CuiTextComponent : ICuiComponent, ICuiColor, ICuiEnableable, ICuiGraphic
    {
        public string Type => "UnityEngine.UI.Text";

        // The string value this text will display.
        [JsonProperty("text")]
        public string Text { get; set; }

        // The size that the Font should render at
        [JsonProperty("fontSize")]
        public int FontSize { get; set; }

        // The Font used by the text
        [JsonProperty("font")]
        public string Font { get; set; }

        // The positioning of the text relative to its RectTransform
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("align")]
        public TextAnchor Align { get; set; }

        public string Color { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("verticalOverflow")]
        public VerticalWrapMode VerticalOverflow { get; set; }

        public float FadeIn { get; set; }

        public string PlaceholderParentId { get; set; }

        public bool? Enabled { get; set; }
    }

    public class CuiImageComponent : ICuiComponent, ICuiColor, ICuiEnableable, ICuiGraphic
    {
        public string Type => "UnityEngine.UI.Image";

        [JsonProperty("sprite")]
        public string Sprite { get; set; }

        [JsonProperty("material")]
        public string Material { get; set; }

        public string Color { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("imagetype")]
        public Image.Type ImageType { get; set; }

        [JsonProperty("fillCenter")]
        public bool? FillCenter { get; set; }

        [JsonProperty("png")]
        public string Png { get; set; }

        [JsonProperty("slice")]
        public string Slice { get; set; }

        [JsonProperty("itemid")]
        public int ItemId { get; set; }

        [JsonProperty("skinid")]
        public ulong SkinId { get; set; }

        public float FadeIn { get; set; }

        public string PlaceholderParentId { get; set; }

        public bool? Enabled { get; set; }
    }

    public class CuiRawImageComponent : ICuiComponent, ICuiColor, ICuiEnableable, ICuiGraphic
    {
        public string Type => "UnityEngine.UI.RawImage";

        [JsonProperty("sprite")]
        public string Sprite { get; set; }

        public string Color { get; set; }

        [JsonProperty("material")]
        public string Material { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("png")]
        public string Png { get; set; }

        [JsonProperty("steamid")]
        public string SteamId { get; set; }

        public float FadeIn { get; set; }

        public string PlaceholderParentId { get; set; }

        public bool? Enabled { get; set; }
    }

    public class CuiButtonComponent : ICuiComponent, ICuiColor, ICuiEnableable, ICuiGraphic
    {
        public string Type => "UnityEngine.UI.Button";

        [JsonProperty("command")]
        public string Command { get; set; }

        [JsonProperty("close")]
        public string Close { get; set; }

        // The sprite that is used to render this image
        [JsonProperty("sprite")]
        public string Sprite { get; set; }

        // The Material set by the player
        [JsonProperty("material")]
        public string Material { get; set; }

        public string Color { get; set; }

        // How the Image is drawn
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("imagetype")]
        public Image.Type ImageType { get; set; }

        [JsonProperty("normalColor")]
        public string NormalColor { get; set; }

        [JsonProperty("highlightedColor")]
        public string HighlightedColor { get; set; }

        [JsonProperty("pressedColor")]
        public string PressedColor { get; set; }

        [JsonProperty("selectedColor")]
        public string SelectedColor { get; set; }

        [JsonProperty("disabledColor")]
        public string DisabledColor { get; set; }

        [JsonProperty("colorMultiplier")]
        public float ColorMultiplier { get; set; }

        [JsonProperty("fadeDuration")]
        public float FadeDuration { get; set; }

        public float FadeIn { get; set; }

        public string PlaceholderParentId { get; set; }

        public bool? Enabled { get; set; }
    }

    public class CuiOutlineComponent : ICuiComponent, ICuiColor, ICuiEnableable
    {
        public string Type => "UnityEngine.UI.Outline";

        // Color for the effect
        public string Color { get; set; }

        // How far is the shadow from the graphic
        [JsonProperty("distance")]
        public string Distance { get; set; }

        // Should the shadow inherit the alpha from the graphic
        [JsonProperty("useGraphicAlpha")]
        public bool UseGraphicAlpha { get; set; }

        public bool? Enabled { get; set; }
    }

    public class CuiInputFieldComponent : ICuiComponent, ICuiColor, ICuiEnableable, ICuiGraphic
    {
        public string Type => "UnityEngine.UI.InputField";

        // The string value this text will display
        [JsonProperty("text")]
        public string Text { get; set; } = string.Empty;

        // The size that the Font should render at
        [JsonProperty("fontSize")]
        public int FontSize { get; set; }

        // The Font used by the text
        [JsonProperty("font")]
        public string Font { get; set; }

        // The positioning of the text relative to its RectTransform
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("align")]
        public TextAnchor Align { get; set; }

        public string Color { get; set; }

        [JsonProperty("characterLimit")]
        public int CharsLimit { get; set; }

        [JsonProperty("command")]
        public string Command { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("lineType")]
        public InputField.LineType LineType { get; set; }

        [JsonProperty("readOnly", DefaultValueHandling = DefaultValueHandling.Include)]
        public bool ReadOnly { get; set; }

        [JsonProperty("placeholderId")]
        private string PlaceholderId { get; set; }

        [JsonProperty("password", DefaultValueHandling = DefaultValueHandling.Include)]
        public bool IsPassword { get; set; }

        [JsonProperty("needsKeyboard", DefaultValueHandling = DefaultValueHandling.Include)]
        public bool NeedsKeyboard { get; set; }

        [JsonProperty("hudMenuInput", DefaultValueHandling = DefaultValueHandling.Include)]
        public bool HudMenuInput { get; set; }

        [JsonProperty("autofocus")]
        public bool Autofocus { get; set; }

        public float FadeIn { get; set; }

        public string PlaceholderParentId { get; set; }

        public bool? Enabled { get; set; }
    }

    public class CuiCountdownComponent : ICuiComponent, ICuiEnableable
    {
        public string Type => "Countdown";

        [JsonProperty("endTime")]
        public float EndTime { get; set; }

        [JsonProperty("startTime")]
        public float StartTime { get; set; }

        [JsonProperty("step")]
        public float Step { get; set; }

        [JsonProperty("interval")]
        public float Interval { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("timerFormat")]
        public TimerFormat TimerFormat { get; set; }

        [JsonProperty("numberFormat")]
        public string NumberFormat { get; set; }

        [JsonProperty("destroyIfDone", DefaultValueHandling = DefaultValueHandling.Include)]
        public bool DestroyIfDone { get; set; }

        [JsonProperty("command")]
        public string Command { get; set; }

        [JsonProperty("fadeIn")]
        public float FadeIn { get; set; }

        public bool? Enabled { get; set; }
    }

    public abstract class CuiLayoutGroupComponent : ICuiComponent, ICuiEnableable
    {
        public abstract string Type { get; }

        [JsonProperty("spacing")]
        public float Spacing { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("childAlignment")]
        public TextAnchor ChildAlignment { get; set; }

        [JsonProperty("childForceExpandWidth")]
        public bool? ChildForceExpandWidth { get; set; }

        [JsonProperty("childForceExpandHeight")]
        public bool? ChildForceExpandHeight { get; set; }

        [JsonProperty("childControlWidth")]
        public bool? ChildControlWidth { get; set; }

        [JsonProperty("childControlHeight")]
        public bool? ChildControlHeight { get; set; }

        [JsonProperty("childScaleWidth")]
        public bool? ChildScaleWidth { get; set; }

        [JsonProperty("childScaleHeight")]
        public bool? ChildScaleHeight { get; set; }

        [JsonProperty("padding")]
        public string Padding { get; set; }

        public bool? Enabled { get; set; }
    }

    public class CuiHorizontalLayoutGroupComponent : CuiLayoutGroupComponent
    {
        public override string Type => "UnityEngine.UI.HorizontalLayoutGroup";
    }

    public class CuiVerticalLayoutGroupComponent : CuiLayoutGroupComponent
    {
        public override string Type => "UnityEngine.UI.VerticalLayoutGroup";
    }

    public class CuiGridLayoutGroupComponent : ICuiComponent, ICuiEnableable
    {
        public string Type => "UnityEngine.UI.GridLayoutGroup";

        [JsonProperty("cellSize")]
        public string CellSize { get; set; }

        [JsonProperty("spacing")]
        public string Spacing { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("startCorner")]
        public GridLayoutGroup.Corner StartCorner { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("startAxis")]
        public GridLayoutGroup.Axis StartAxis { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("childAlignment")]
        public TextAnchor ChildAlignment { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("constraint")]
        public GridLayoutGroup.Constraint Constraint { get; set; }

        [JsonProperty("constraintCount")]
        public int ConstraintCount { get; set; }

        [JsonProperty("padding")]
        public string Padding { get; set; }

        public bool? Enabled { get; set; }
    }

    public class CuiContentSizeFitterComponent : ICuiComponent, ICuiEnableable
    {
        public string Type => "UnityEngine.UI.ContentSizeFitter";

        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("horizontalFit")]
        public ContentSizeFitter.FitMode HorizontalFit { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("verticalFit")]
        public ContentSizeFitter.FitMode VerticalFit { get; set; }

        public bool? Enabled { get; set; }
    }

    public class CuiLayoutElementComponent : ICuiComponent, ICuiEnableable
    {
        public string Type => "UnityEngine.UI.LayoutElement";

        [JsonProperty("preferredWidth")]
        public float PreferredWidth { get; set; }

        [JsonProperty("preferredHeight")]
        public float PreferredHeight { get; set; }

        [JsonProperty("minWidth")]
        public float MinWidth { get; set; }

        [JsonProperty("minHeight")]
        public float MinHeight { get; set; }

        [JsonProperty("flexibleWidth")]
        public float FlexibleWidth { get; set; }

        [JsonProperty("flexibleHeight")]
        public float FlexibleHeight { get; set; }

        [JsonProperty("ignoreLayout")]
        public bool? IgnoreLayout { get; set; }

        public bool? Enabled { get; set; }
    }

    public class CuiDraggableComponent : ICuiComponent, ICuiEnableable
    {
        public string Type => "Draggable";

        [JsonProperty("limitToParent")]
        public bool? LimitToParent { get; set; }

        [JsonProperty("maxDistance")]
        public float MaxDistance { get; set; }

        [JsonProperty("allowSwapping")]
        public bool? AllowSwapping { get; set; }

        [JsonProperty("dropAnywhere")]
        public bool? DropAnywhere { get; set; }

        [JsonProperty("dragAlpha")]
        public float DragAlpha { get; set; }

        [JsonProperty("parentLimitIndex")]
        public int ParentLimitIndex { get; set; }

        [JsonProperty("filter")]
        public string Filter { get; set; }

        [JsonProperty("parentPadding")]
        public string ParentPadding { get; set; }

        [JsonProperty("anchorOffset")]
        public string AnchorOffset { get; set; }

        [JsonProperty("keepOnTop")]
        public bool? KeepOnTop { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("positionRPC")]
        public CommunityEntity.DraggablePositionSendType PositionRPC { get; set; }

        [JsonProperty("moveToAnchor")]
        public bool MoveToAnchor { get; set; }

        [JsonProperty("rebuildAnchor")]
        public bool RebuildAnchor { get; set; }

        public bool? Enabled { get; set; }
    }

    public class CuiSlotComponent : ICuiComponent, ICuiEnableable
    {
        public string Type => "Slot";

        [JsonProperty("filter")]
        public string Filter { get; set; }

        public bool? Enabled { get; set; }
    }

    public enum TimerFormat
    {
        None,
        SecondsHundreth,
        MinutesSeconds,
        MinutesSecondsHundreth,
        HoursMinutes,
        HoursMinutesSeconds,
        HoursMinutesSecondsMilliseconds,
        HoursMinutesSecondsTenths,
        DaysHoursMinutes,
        DaysHoursMinutesSeconds,
        Custom
    }

    public class CuiNeedsCursorComponent : ICuiComponent, ICuiEnableable
    {
        public string Type => "NeedsCursor";

        public bool? Enabled { get; set; }
    }

    public class CuiNeedsKeyboardComponent : ICuiComponent, ICuiEnableable
    {
        public string Type => "NeedsKeyboard";

        public bool? Enabled { get; set; }
    }

    public class CuiRectTransformComponent : CuiRectTransform, ICuiComponent
    {
        public string Type => "RectTransform";
    }

    public class CuiRectTransform
    {
        // The normalized position in the parent RectTransform that the lower left corner is anchored to
        [JsonProperty("anchormin")]
        public string AnchorMin { get; set; }

        // The normalized position in the parent RectTransform that the upper right corner is anchored to
        [JsonProperty("anchormax")]
        public string AnchorMax { get; set; }

        // The offset of the lower left corner of the rectangle relative to the lower left anchor
        [JsonProperty("offsetmin")]
        public string OffsetMin { get; set; }

        // The offset of the upper right corner of the rectangle relative to the upper right anchor
        [JsonProperty("offsetmax")]
        public string OffsetMax { get; set; }

        /// <remarks>
        /// Only works in CuiRectTransformComponent not in CuiScrollViewComponent.ContentTransform
        /// </remarks>
        [JsonProperty("rotation")]
        public float Rotation { get; set; }

        [JsonProperty("pivot")]
        public string Pivot { get; set; }

        /// <remarks>
        /// Only works in CuiRectTransformComponent not in CuiScrollViewComponent.ContentTransform
        /// </remarks>
        [JsonProperty("setParent")]
        public string SetParent { get; set; }

        /// <remarks>
        /// Only works in CuiRectTransformComponent not in CuiScrollViewComponent.ContentTransform
        /// </remarks>
        [JsonProperty("setTransformIndex")]
        public int SetTransformIndex { get; set; }
    }

    public class CuiScrollViewComponent : ICuiComponent, ICuiEnableable
    {
        public string Type => "UnityEngine.UI.ScrollView";

        [JsonProperty("contentTransform")]
        public CuiRectTransform ContentTransform { get; set; }

        [JsonProperty("horizontal", DefaultValueHandling = DefaultValueHandling.Include)]
        public bool Horizontal { get; set; }

        [JsonProperty("vertical", DefaultValueHandling = DefaultValueHandling.Include)]
        public bool Vertical { get; set; }

        [JsonProperty("movementType")]
        [JsonConverter(typeof(StringEnumConverter))]
        public ScrollRect.MovementType MovementType { get; set; }

        [JsonProperty("elasticity")]
        public float Elasticity { get; set; }

        [JsonProperty("inertia", DefaultValueHandling = DefaultValueHandling.Include)]
        public bool Inertia { get; set; }

        [JsonProperty("decelerationRate")]
        public float DecelerationRate { get; set; }

        [JsonProperty("scrollSensitivity")]
        public float ScrollSensitivity { get; set; }

        [JsonProperty("horizontalScrollbar")]
        public CuiScrollbar HorizontalScrollbar { get; set; }

        [JsonProperty("verticalScrollbar")]
        public CuiScrollbar VerticalScrollbar { get; set; }

        [JsonProperty("horizontalNormalizedPosition")]
        public float? HorizontalNormalizedPosition { get; set; }

        [JsonProperty("verticalNormalizedPosition")]
        public float? VerticalNormalizedPosition { get; set; }

        public bool? Enabled { get; set; }
    }

    public class CuiScrollbar : ICuiEnableable
    {
        [JsonProperty("invert")]
        public bool Invert { get; set; }

        [JsonProperty("autoHide")]
        public bool AutoHide { get; set; }

        [JsonProperty("handleSprite")]
        public string HandleSprite { get; set; }

        [JsonProperty("size")]
        public float Size { get; set; }

        [JsonProperty("handleColor")]
        public string HandleColor { get; set; }

        [JsonProperty("highlightColor")]
        public string HighlightColor { get; set; }

        [JsonProperty("pressedColor")]
        public string PressedColor { get; set; }

        [JsonProperty("trackSprite")]
        public string TrackSprite { get; set; }

        [JsonProperty("trackColor")]
        public string TrackColor { get; set; }

        public bool? Enabled { get; set; }
    }

    public class ComponentConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);
            string typeName = jObject["type"].ToString();
            Type type;

            switch (typeName)
            {
                case "UnityEngine.UI.Text":
                    type = typeof(CuiTextComponent);
                    break;

                case "UnityEngine.UI.Image":
                    type = typeof(CuiImageComponent);
                    break;

                case "UnityEngine.UI.RawImage":
                    type = typeof(CuiRawImageComponent);
                    break;

                case "UnityEngine.UI.Button":
                    type = typeof(CuiButtonComponent);
                    break;

                case "UnityEngine.UI.Outline":
                    type = typeof(CuiOutlineComponent);
                    break;

                case "UnityEngine.UI.InputField":
                    type = typeof(CuiInputFieldComponent);
                    break;

                case "Countdown":
                    type = typeof(CuiCountdownComponent);
                    break;

                case "UnityEngine.UI.HorizontalLayoutGroup":
                    type = typeof(CuiHorizontalLayoutGroupComponent);
                    break;

                case "UnityEngine.UI.VerticalLayoutGroup":
                    type = typeof(CuiVerticalLayoutGroupComponent);
                    break;

                case "UnityEngine.UI.GridLayoutGroup":
                    type = typeof(CuiGridLayoutGroupComponent);
                    break;

                case "UnityEngine.UI.ContentSizeFitter":
                    type = typeof(CuiContentSizeFitterComponent);
                    break;

                case "UnityEngine.UI.LayoutElement":
                    type = typeof(CuiLayoutElementComponent);
                    break;

                case "Draggable":
                    type = typeof(CuiDraggableComponent);
                    break;

                case "Slot":
                    type = typeof(CuiSlotComponent);
                    break;

                case "NeedsCursor":
                    type = typeof(CuiNeedsCursorComponent);
                    break;

                case "NeedsKeyboard":
                    type = typeof(CuiNeedsKeyboardComponent);
                    break;

                case "RectTransform":
                    type = typeof(CuiRectTransformComponent);
                    break;

                case "UnityEngine.UI.ScrollView":
                    type = typeof(CuiScrollViewComponent);
                    break;

                default:
                    return null;
            }

            object target = Activator.CreateInstance(type);
            serializer.Populate(jObject.CreateReader(), target);
            return target;
        }

        public override bool CanConvert(Type objectType) => objectType == typeof(ICuiComponent);

        public override bool CanWrite => false;
    }
}
