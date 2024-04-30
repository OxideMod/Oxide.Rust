extern alias References;

using Oxide.Core;
using References::Newtonsoft.Json;
using References::Newtonsoft.Json.Converters;
using References::Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TinyJSON;
using UnityEngine;
using UnityEngine.UI;

namespace Oxide.Game.Rust.Cui
{
    public static class CuiHelper
    {
        public static string ToJson(List<CuiElement> elements, bool format = false)
        {
            StringBuilder stringBuilder = new StringBuilder();
            using (StringWriter stringWriter = new StringWriter(stringBuilder))
            {
                using (JsonWriter jsonWriter = new JsonTextWriter(stringWriter))
                {
                    jsonWriter.Formatting = Formatting.None;

                    if (elements.Count > 0)
                    {
                        jsonWriter.WriteStartArray();
                        foreach (var element in elements)
                        {
                            element.WriteJson(jsonWriter);
                        }
                        jsonWriter.WriteEndArray();
                    }
                }
            }
            return stringBuilder.Replace("\\n", "\n").ToString();
        }


        public static List<CuiElement> FromJson(string json) => JsonConvert.DeserializeObject<List<CuiElement>>(json);

        public static string GetGuid() => Guid.NewGuid().ToString().Replace("-", string.Empty);

        public static bool AddUi(BasePlayer player, List<CuiElement> elements) => AddUi(player, ToJson(elements));

        public static bool AddUi(BasePlayer player, string json)
        {
            if (player?.net != null && Interface.CallHook("CanUseUI", player, json) == null)
            {
                CommunityEntity.ServerInstance.ClientRPC(RpcTarget.Player("AddUI", player.net.connection), json);
                return true;
            }

            return false;
        }

        public static bool AddUi(List<Network.Connection> playerList, List<CuiElement> elements)
        {
            CommunityEntity.ServerInstance.ClientRPC(RpcTarget.Players("AddUI", playerList), ToJson(elements));
            return true;
        }

        public static bool AddUi(List<BasePlayer> playerList, string json)
        {
            List<Network.Connection> connections = new List<Network.Connection>();
            foreach (var player in playerList)
            {
                if (player?.net != null)
                {
                    connections.Add(player.net.connection);
                }
            }

            CommunityEntity.ServerInstance.ClientRPC(RpcTarget.Players("AddUI", connections), json);
            return true;
        }

        public static bool AddUi(List<BasePlayer> playerList, List<CuiElement> elements) => AddUi(playerList, ToJson(elements));

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

        [JsonProperty("destroyUi", NullValueHandling = NullValueHandling.Ignore)]
        public string DestroyUi { get; set; }

        [JsonProperty("components")]
        public List<ICuiComponent> Components { get; } = new List<ICuiComponent>();

        [JsonProperty("fadeOut")]
        public float FadeOut { get; set; }

        [JsonProperty("update", NullValueHandling = NullValueHandling.Ignore)]
        public bool Update { get; set; }

        public void WriteJson(JsonWriter jsonWriter)
        {
            jsonWriter.WriteStartObject();

            if (Name != null)
            {
                jsonWriter.WritePropertyName("name");
                jsonWriter.WriteValue(Name);
            }

            if (Parent != null)
            {
                jsonWriter.WritePropertyName("parent");
                jsonWriter.WriteValue(Parent);
            }

            if (DestroyUi != null)
            {
                jsonWriter.WritePropertyName("destroyUi");
                jsonWriter.WriteValue(DestroyUi);
            }

            if (FadeOut != 0f)
            {
                jsonWriter.WritePropertyName("fadeOut");
                jsonWriter.WriteValue(FadeOut);
            }

            if (Update)
            {
                jsonWriter.WritePropertyName("update");
                jsonWriter.WriteValue(Update);
            }

            if (Components.Count > 0)
            {
                jsonWriter.WritePropertyName("components");
                jsonWriter.WriteStartArray();

                foreach (var component in Components)
                {
                    component.WriteJson(jsonWriter);
                }

                jsonWriter.WriteEndArray();
            }

            jsonWriter.WriteEndObject();
        }
    }

    [JsonConverter(typeof(ComponentConverter))]
    public interface ICuiComponent
    {
        [JsonProperty("type")]
        string Type { get; }
        void WriteJson(JsonWriter jsonWriter);
    }

    public interface ICuiColor
    {
        [JsonProperty("color")]
        string Color { get; set; }
    }

    public class CuiTextComponent : ICuiComponent, ICuiColor
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

        [JsonProperty("fadeIn")]
        public float FadeIn { get; set; }

        public void WriteJson(JsonWriter jsonWriter)
        {
            jsonWriter.WriteStartObject();

            jsonWriter.WritePropertyName("type");
            jsonWriter.WriteValue("UnityEngine.UI.Text");

            if (Text != null)
            {
                jsonWriter.WritePropertyName("text");
                jsonWriter.WriteValue(Text);
            }

            if (Font != null)
            {
                jsonWriter.WritePropertyName("font");
                jsonWriter.WriteValue(Font);
            }

            if (FontSize != 0)
            {
                jsonWriter.WritePropertyName("fontSize");
                jsonWriter.WriteValue(FontSize);
            }

            if (Align != default(TextAnchor))
            {
                jsonWriter.WritePropertyName("align");
                jsonWriter.WriteValue(Align.ToString());
            }

            if (Color != null)
            {
                jsonWriter.WritePropertyName("color");
                jsonWriter.WriteValue(Color);
            }

            if (VerticalOverflow != VerticalWrapMode.Truncate)
            {
                jsonWriter.WritePropertyName("verticalOverflow");
                jsonWriter.WriteValue(VerticalOverflow.ToString());
            }

            if (FadeIn > 0f)
            {
                jsonWriter.WritePropertyName("fadeIn");
                jsonWriter.WriteValue(FadeIn);
            }

            jsonWriter.WriteEndObject();
        }
    }

    public class CuiImageComponent : ICuiComponent, ICuiColor
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

        [JsonProperty("png")]
        public string Png { get; set; }

        [JsonProperty("fadeIn")]
        public float FadeIn { get; set; }

        [JsonProperty("itemid")]
        public int ItemId { get; set; }

        [JsonProperty("skinid")]
        public ulong SkinId { get; set; }

        public void WriteJson(JsonWriter jsonWriter)
        {
            jsonWriter.WriteStartObject();

            jsonWriter.WritePropertyName("type");
            jsonWriter.WriteValue("UnityEngine.UI.Image");

            if (Sprite != null)
            {
                jsonWriter.WritePropertyName("sprite");
                jsonWriter.WriteValue(Sprite);
            }

            if (Material != null)
            {
                jsonWriter.WritePropertyName("material");
                jsonWriter.WriteValue(Material);
            }

            if (Color != null)
            {
                jsonWriter.WritePropertyName("color");
                jsonWriter.WriteValue(Color);
            }

            if (ImageType != default(Image.Type))
            {
                jsonWriter.WritePropertyName("imagetype");
                jsonWriter.WriteValue(ImageType.ToString());
            }

            if (Png != null)
            {
                jsonWriter.WritePropertyName("png");
                jsonWriter.WriteValue(Png);
            }

            if (FadeIn > 0f)
            {
                jsonWriter.WritePropertyName("fadeIn");
                jsonWriter.WriteValue(FadeIn);
            }

            if (ItemId != 0)
            {
                jsonWriter.WritePropertyName("itemid");
                jsonWriter.WriteValue(ItemId);
            }

            if (SkinId != 0)
            {
                jsonWriter.WritePropertyName("skinid");
                jsonWriter.WriteValue(SkinId);
            }

            jsonWriter.WriteEndObject();
        }
    }

    public class CuiRawImageComponent : ICuiComponent, ICuiColor
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

        [JsonProperty("fadeIn")]
        public float FadeIn { get; set; }

        public void WriteJson(JsonWriter jsonWriter)
        {
            jsonWriter.WriteStartObject();

            jsonWriter.WritePropertyName("type");
            jsonWriter.WriteValue("UnityEngine.UI.RawImage");

            if (Sprite != null)
            {
                jsonWriter.WritePropertyName("sprite");
                jsonWriter.WriteValue(Sprite);
            }

            if (Color != null)
            {
                jsonWriter.WritePropertyName("color");
                jsonWriter.WriteValue(Color);
            }

            if (Material != null)
            {
                jsonWriter.WritePropertyName("material");
                jsonWriter.WriteValue(Material);
            }

            if (Url != null)
            {
                jsonWriter.WritePropertyName("url");
                jsonWriter.WriteValue(Url);
            }

            if (Png != null)
            {
                jsonWriter.WritePropertyName("png");
                jsonWriter.WriteValue(Png);
            }

            if (FadeIn > 0f)
            {
                jsonWriter.WritePropertyName("fadeIn");
                jsonWriter.WriteValue(FadeIn);
            }

            jsonWriter.WriteEndObject();
        }
    }

    public class CuiButtonComponent : ICuiComponent, ICuiColor
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

        // How the Image is draw
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("imagetype")]
        public Image.Type ImageType { get; set; }

        [JsonProperty("fadeIn")]
        public float FadeIn { get; set; }

        public void WriteJson(JsonWriter jsonWriter)
        {
            jsonWriter.WriteStartObject();

            jsonWriter.WritePropertyName("type");
            jsonWriter.WriteValue("UnityEngine.UI.Button");

            if (Command != null)
            {
                jsonWriter.WritePropertyName("command");
                jsonWriter.WriteValue(Command);
            }

            if (Close != null)
            {
                jsonWriter.WritePropertyName("close");
                jsonWriter.WriteValue(Close);
            }

            if (Sprite != null)
            {
                jsonWriter.WritePropertyName("sprite");
                jsonWriter.WriteValue(Sprite);
            }

            if (Material != null)
            {
                jsonWriter.WritePropertyName("material");
                jsonWriter.WriteValue(Material);
            }

            if (Color != null)
            {
                jsonWriter.WritePropertyName("color");
                jsonWriter.WriteValue(Color);
            }

            if (ImageType != default(Image.Type))
            {
                jsonWriter.WritePropertyName("imagetype");
                jsonWriter.WriteValue(ImageType.ToString());
            }

            if (FadeIn > 0f)
            {
                jsonWriter.WritePropertyName("fadeIn");
                jsonWriter.WriteValue(FadeIn);
            }

            jsonWriter.WriteEndObject();
        }
    }

    public class CuiOutlineComponent : ICuiComponent, ICuiColor
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

        public void WriteJson(JsonWriter jsonWriter)
        {
            jsonWriter.WriteStartObject();

            jsonWriter.WritePropertyName("type");
            jsonWriter.WriteValue("UnityEngine.UI.Outline");

            if (Color != null)
            {
                jsonWriter.WritePropertyName("color");
                jsonWriter.WriteValue(Color);
            }

            if (Distance != null)
            {
                jsonWriter.WritePropertyName("distance");
                jsonWriter.WriteValue(Distance);
            }

            if (UseGraphicAlpha)
            {
                jsonWriter.WritePropertyName("useGraphicAlpha");
                jsonWriter.WriteValue(UseGraphicAlpha);
            }

            jsonWriter.WriteEndObject();
        }
    }

    public class CuiInputFieldComponent : ICuiComponent, ICuiColor
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

        [JsonProperty("password")]
        public bool IsPassword { get; set; }

        [JsonProperty("readOnly")]
        public bool ReadOnly { get; set; }

        [JsonProperty("needsKeyboard")]
        public bool NeedsKeyboard { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("lineType")]
        public InputField.LineType LineType { get; set; }

        [JsonProperty("autofocus")]
        public bool Autofocus { get; set; }

        [JsonProperty("hudMenuInput")]
        public bool HudMenuInput { get; set; }

        public void WriteJson(JsonWriter jsonWriter)
        {
            jsonWriter.WriteStartObject();

            jsonWriter.WritePropertyName("type");
            jsonWriter.WriteValue("UnityEngine.UI.InputField");

            if (Text != null)
            {
                jsonWriter.WritePropertyName("text");
                jsonWriter.WriteValue(Text);
            }

            if (FontSize != 0)
            {
                jsonWriter.WritePropertyName("fontSize");
                jsonWriter.WriteValue(FontSize);
            }

            if (Font != null)
            {
                jsonWriter.WritePropertyName("font");
                jsonWriter.WriteValue(Font);
            }

            if (Align != default(TextAnchor))
            {
                jsonWriter.WritePropertyName("align");
                jsonWriter.WriteValue(Align.ToString());
            }

            if (Color != null)
            {
                jsonWriter.WritePropertyName("color");
                jsonWriter.WriteValue(Color);
            }

            if (CharsLimit != 0)
            {
                jsonWriter.WritePropertyName("characterLimit");
                jsonWriter.WriteValue(CharsLimit);
            }

            if (Command != null)
            {
                jsonWriter.WritePropertyName("command");
                jsonWriter.WriteValue(Command);
            }

            if (IsPassword)
            {
                jsonWriter.WritePropertyName("password");
                jsonWriter.WriteValue(IsPassword);
            }

            if (ReadOnly)
            {
                jsonWriter.WritePropertyName("readOnly");
                jsonWriter.WriteValue(ReadOnly);
            }

            if (NeedsKeyboard)
            {
                jsonWriter.WritePropertyName("needsKeyboard");
                jsonWriter.WriteValue(NeedsKeyboard);
            }

            if (LineType != default(InputField.LineType))
            {
                jsonWriter.WritePropertyName("lineType");
                jsonWriter.WriteValue(LineType.ToString());
            }

            if (Autofocus)
            {
                jsonWriter.WritePropertyName("autofocus");
                jsonWriter.WriteValue(Autofocus);
            }

            if (HudMenuInput)
            {
                jsonWriter.WritePropertyName("hudMenuInput");
                jsonWriter.WriteValue(HudMenuInput);
            }

            jsonWriter.WriteEndObject();
        }
    }

    public class CuiCountdownComponent : ICuiComponent
    {
        public string Type => "Countdown";

        [JsonProperty("endTime")]
        public int EndTime { get; set; }

        [JsonProperty("startTime")]
        public int StartTime { get; set; }

        [JsonProperty("step")]
        public int Step { get; set; }

        [JsonProperty("command")]
        public string Command { get; set; }

        [JsonProperty("fadeIn")]
        public float FadeIn { get; set; }

        public void WriteJson(JsonWriter jsonWriter)
        {
            jsonWriter.WriteStartObject();

            jsonWriter.WritePropertyName("type");
            jsonWriter.WriteValue("Countdown");

            if (EndTime != 0)
            {
                jsonWriter.WritePropertyName("endTime");
                jsonWriter.WriteValue(EndTime);
            }

            if (StartTime != 0)
            {
                jsonWriter.WritePropertyName("startTime");
                jsonWriter.WriteValue(StartTime);
            }

            if (Step != 1)
            {
                jsonWriter.WritePropertyName("step");
                jsonWriter.WriteValue(Step);
            }

            if (Command != null)
            {
                jsonWriter.WritePropertyName("command");
                jsonWriter.WriteValue(Command);
            }

            if (FadeIn > 0f)
            {
                jsonWriter.WritePropertyName("fadeIn");
                jsonWriter.WriteValue(FadeIn);
            }

            jsonWriter.WriteEndObject();
        }
    }

    public class CuiNeedsCursorComponent : ICuiComponent
    {
        public string Type => "NeedsCursor";
        public void WriteJson(JsonWriter jsonWriter)
        {
            jsonWriter.WriteStartObject();

            jsonWriter.WritePropertyName("type");
            jsonWriter.WriteValue("NeedsCursor");

            jsonWriter.WriteEndObject();
        }
    }

    public class CuiNeedsKeyboardComponent : ICuiComponent
    {
        public string Type => "NeedsKeyboard";
        public void WriteJson(JsonWriter jsonWriter)
        {
            jsonWriter.WriteStartObject();

            jsonWriter.WritePropertyName("type");
            jsonWriter.WriteValue("NeedsKeyboard");

            jsonWriter.WriteEndObject();
        }
    }

    public class CuiRectTransformComponent : ICuiComponent
    {
        public string Type => "RectTransform";

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

        public void WriteJson(JsonWriter jsonWriter)
        {
            jsonWriter.WriteStartObject();
            jsonWriter.WritePropertyName("type");
            jsonWriter.WriteValue("RectTransform");

            if (AnchorMin != null)
            {
                jsonWriter.WritePropertyName("anchormin");
                jsonWriter.WriteValue(AnchorMin);
            }

            if (AnchorMax != null)
            {
                jsonWriter.WritePropertyName("anchormax");
                jsonWriter.WriteValue(AnchorMax);
            }

            if (OffsetMin != null)
            {
                jsonWriter.WritePropertyName("offsetmin");
                jsonWriter.WriteValue(OffsetMin);
            }

            if (OffsetMax != null)
            {
                jsonWriter.WritePropertyName("offsetmax");
                jsonWriter.WriteValue(OffsetMax);
            }

            jsonWriter.WriteEndObject();
        }
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

                case "NeedsCursor":
                    type = typeof(CuiNeedsCursorComponent);
                    break;

                case "NeedsKeyboard":
                    type = typeof(CuiNeedsKeyboardComponent);
                    break;

                case "RectTransform":
                    type = typeof(CuiRectTransformComponent);
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
