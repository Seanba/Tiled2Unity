// WARNING
//
// This file has been generated automatically by Xamarin Studio to store outlets and
// actions made in the UI designer. If it is removed, they will be lost.
// Manual changes to this file may not be handled correctly.
//
using Foundation;
using System.CodeDom.Compiler;

namespace Tiled2UnityMac
{
	[Register ("ViewController")]
	partial class ViewController
	{
		[Outlet]
		AppKit.NSButton CheckButtonDepthBufferEnabled { get; set; }

		[Outlet]
		AppKit.NSButton CheckButtonPreferConvexPolygons { get; set; }

		[Outlet]
		AppKit.NSTextField TextFieldExportTo { get; set; }

		[Outlet]
		AppKit.NSTextField TextFieldLaunchTip { get; set; }

		[Outlet]
		AppKit.NSTextField TextFieldScale { get; set; }

		[Outlet]
		AppKit.NSTextField TextViewObjectTypesXml { get; set; }

		[Outlet]
		AppKit.NSTextView TextViewOutput { get; set; }

		[Action ("ButtonClicked_BigAssExport:")]
		partial void ButtonClicked_BigAssExport (Foundation.NSObject sender);

		[Action ("ClickedButton_ClearObjectXml:")]
		partial void ClickedButton_ClearObjectXml (Foundation.NSObject sender);

		[Action ("ClickedButton_DepthBufferEnabled:")]
		partial void ClickedButton_DepthBufferEnabled (Foundation.NSObject sender);

		[Action ("ClickedButton_ExportTo:")]
		partial void ClickedButton_ExportTo (Foundation.NSObject sender);

		[Action ("ClickedButton_ObjectTypesXml:")]
		partial void ClickedButton_ObjectTypesXml (Foundation.NSObject sender);

		[Action ("ClickedButton_PreferConvexPolygons:")]
		partial void ClickedButton_PreferConvexPolygons (Foundation.NSObject sender);

		[Action ("ClickedButton_Preview:")]
		partial void ClickedButton_Preview (Foundation.NSObject sender);

		[Action ("TextChanged_Scale:")]
		partial void TextChanged_Scale (Foundation.NSObject sender);
		
		void ReleaseDesignerOutlets ()
		{
			if (CheckButtonDepthBufferEnabled != null) {
				CheckButtonDepthBufferEnabled.Dispose ();
				CheckButtonDepthBufferEnabled = null;
			}

			if (CheckButtonPreferConvexPolygons != null) {
				CheckButtonPreferConvexPolygons.Dispose ();
				CheckButtonPreferConvexPolygons = null;
			}

			if (TextFieldExportTo != null) {
				TextFieldExportTo.Dispose ();
				TextFieldExportTo = null;
			}

			if (TextFieldLaunchTip != null) {
				TextFieldLaunchTip.Dispose ();
				TextFieldLaunchTip = null;
			}

			if (TextFieldScale != null) {
				TextFieldScale.Dispose ();
				TextFieldScale = null;
			}

			if (TextViewObjectTypesXml != null) {
				TextViewObjectTypesXml.Dispose ();
				TextViewObjectTypesXml = null;
			}

			if (TextViewOutput != null) {
				TextViewOutput.Dispose ();
				TextViewOutput = null;
			}
		}
	}
}
