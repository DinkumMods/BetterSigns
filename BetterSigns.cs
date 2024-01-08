using BepInEx;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using TMPro;

namespace BetterSigns {

	[BepInAutoPlugin]
	public partial class BetterSigns : BaseUnityPlugin {
		private readonly Harmony harmony = new Harmony(Id);
		public static BetterSigns Instance;
		
		public static readonly int CHARACTER_LIMIT = 0xFFF; // 0 for infinite
		public static readonly int COLUMNS = 5;
		public static readonly int ICON_COUNT = 32;

		public static void Log(System.Object log) {
			Instance.Logger.LogInfo(log);
		}

		private void Awake() {
			Instance = this;
			
			this.Patch();
		}
		
		bool patched = false;
		private void Patch() {
			if (patched) return;
			Log("Patching SignWritingWindow");
			var openSignWritingWindow	= typeof(BetterSignPatches).GetMethod("openSignWritingWindow", BindingFlags.Static | BindingFlags.NonPublic);

			harmony.Patch(
				typeof(SignManager).GetMethod("openSignWritingWindow"),
				postfix: new HarmonyMethod(openSignWritingWindow));
				
			patched = true;
		}
	}
	
    class BetterSignPatches {
		
		static TMP_InputField input = null;
		static bool initialized = false;
		
		static Dictionary<string, bool> tags = new Dictionary<string, bool>();

		static void ActivateInputField() {
			if (input == null) return;
			
			if (input.isFocused && input.m_SoftKeyboard != null && !input.m_SoftKeyboard.active) {
				input.m_SoftKeyboard.active = true;
				input.m_SoftKeyboard.text = input.m_Text;
			}
			
			input.m_ShouldActivateNextUpdate = true;
			
			if (EventSystem.current.currentSelectedGameObject != input.gameObject) {
				EventSystem.current.SetSelectedGameObject(input.gameObject);
			}

			input.m_AllowInput = true;
			input.m_OriginalText = input.text;
			input.m_WasCanceled = false;
			input.SetCaretVisible();
			input.UpdateLabel();
		}
		
		static void OnButtonPress(string ico) {
			if (input != null) {
				input.Append(ico);
				ActivateInputField();
			}
		}
		
		private static GameObject GetChildByName(Transform transform, string name) {
			for (int i = 0; i < transform.childCount; i++) {
				var c = transform.GetChild(i);
				if (c.name == name)
					return c.gameObject;
			}
			return null;
		}
		
		readonly struct ExtraButton {
			public readonly string Name, Tag;
			public readonly bool Toggle;
			
			public ExtraButton(string name, string tag, bool toggle) {
				Name = name;
				Tag = tag;
				Toggle = toggle;
			}
		}
		
		public static void Init() {
			if (initialized) return;
			input.characterLimit = BetterSigns.CHARACTER_LIMIT;
			input.m_LineType = TMP_InputField.LineType.MultiLineSubmit;
			input.lineLimit = 5;
			input.SetTextComponentWrapMode();

			GameObject parent = GetChildByName(SignManager.manage.signWritingWindow.transform, "BuyAndNameWindow");
			
			GameObject confirm = GetChildByName(parent.transform, "Confirm");
			
			Image textbox = SignManager.manage.signInput.gameObject.GetComponent<Image>();
			textbox.rectTransform.sizeDelta += new Vector2(0, 80);
			textbox.rectTransform.anchoredPosition -= new Vector2(0, 40);

			GameObject prefab = Object.Instantiate(confirm);
			prefab.name = "Sprite Button";
			prefab.GetComponent<InvButton>().onButtonPress = new UnityEvent(); // Clear Event
			prefab.GetComponent<Image>().rectTransform.sizeDelta = new Vector2(50, 50);

			for (int i = 0; i < BetterSigns.ICON_COUNT; i++) {
				GameObject b = Object.Instantiate(prefab, parent.transform);
				b.name = "Sprite Button " + i;
				TextMeshProUGUI tmp = b.GetComponentInChildren<TextMeshProUGUI>();
				string ico = "<sprite=" + i + ">";
				tmp.SetText(ico);

				b.GetComponent<InvButton>().onButtonPress.AddListener(() => OnButtonPress(ico));
				
				int r = i / BetterSigns.COLUMNS, 
				    c = i % BetterSigns.COLUMNS;
				
				b.transform.position += new Vector3(350 + c * 110, 500 + r * -110, 0);
			}

			System.Func<string, ExtraButton> ColorButton = (c) => new ExtraButton($"<color={c}>#</color>", $"color={c}", false);
			
			ExtraButton[] extras = new ExtraButton[] { 
				new ExtraButton("<b>b</b>", "b", true),
				new ExtraButton("<i>i</i>", "i", true),
				new ExtraButton("<s>st</s>", "s", true),
				new ExtraButton("<u>u</u>", "u", true),
				new ExtraButton("x<sup>y</sup>", "sup", true),
				new ExtraButton("x<sub>y</sub>", "sub", true),
				new ExtraButton("br", "br", false),
				ColorButton("#E00"),
				ColorButton("#0E0"),
				ColorButton("#00E"),
				ColorButton("#FFF"),
				ColorButton("#000"),
				ColorButton("#EE0"),
				new ExtraButton("<i><s>#</s></i>", "/color", false),
			};
			
			int ROWS = ((BetterSigns.ICON_COUNT - 1) / BetterSigns.COLUMNS) + 1;
			
			for (int i = 0; i < extras.Length; i++) {
				ExtraButton ex = extras[i];
				
				GameObject b = Object.Instantiate(prefab, parent.transform);
				b.name = "Extra Button " + i;
				TextMeshProUGUI tmp = b.GetComponentInChildren<TextMeshProUGUI>();
				tmp.SetText(ex.Name);
				
				if (ex.Toggle) {
					string tag = ex.Tag;
					b.GetComponent<InvButton>().onButtonPress.AddListener(() => {
						bool toggled;
						if (!tags.TryGetValue(tag, out toggled)) toggled = false;
						OnButtonPress(toggled ? "</" + tag + ">" : "<" + tag + ">");
						tags[tag] = !toggled;
					});
				} else {
					b.GetComponent<InvButton>().onButtonPress.AddListener(() => OnButtonPress("<" + ex.Tag + ">"));
				}
				
				int c = i / ROWS, 
				    r = i % ROWS;
					
				b.transform.position += new Vector3(-350 + c * -110, 500 + r * -110, 0);
			}
			
			// workaround for enter press
			GameObject clone = Object.Instantiate(confirm, parent.transform);
			confirm.transform.parent = null;
			Object.Destroy(confirm);
			
			clone.name = "Confirm";
			clone.transform.position += new Vector3(0, -155, 0);
			
			initialized = true;
		}
		
		static void openSignWritingWindow(ref SignManager __instance) {
			input = __instance.signInput;
			tags.Clear();
			if (initialized)
				return;
			Init();
        }
    }
}