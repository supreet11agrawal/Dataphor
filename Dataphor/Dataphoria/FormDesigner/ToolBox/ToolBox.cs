﻿using System;
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Alphora.Dataphor.DAE.Runtime.Data;
using Alphora.Dataphor.Frontend.Client;
using Alphora.Dataphor.Frontend.Client.Windows;
using Syncfusion.Windows.Forms.Tools;
using Image=System.Drawing.Image;
using Session=Alphora.Dataphor.Frontend.Client.Windows.Session;

namespace Alphora.Dataphor.Dataphoria.FormDesigner.ToolBox
{
    public partial class ToolBox : UserControl, IErrorSource
    {
        private GroupBar FPaletteGroupBar;
        private GroupView FPointerGroupView;
        //private System.Windows.Forms.ImageList FNodesImageList;


        public ToolBox()
        {
            InitializeComponent();
            InitializeGroupView();
        }

        #region IErrorSource Members

        public void ErrorHighlighted(Exception AException)
        {
        }

        public void ErrorSelected(Exception AException)
        {
            throw new NotImplementedException();
        }

        #endregion

        private EventHandler<EventArgs> FStatusChanged;
        
        private void SetStatus(string ADescription)
        {
            EventHandler<EventArgs> LChanged = FStatusChanged;
            if (LChanged != null) LChanged(this, new StatusEventArgs(ADescription));
        }

        public event EventHandler<EventArgs> StatusChanged
        {
            add { FStatusChanged += value; }
            remove { FStatusChanged -= value; }
        }        


        private void InitializeGroupView()
        {
            // 
            // FPaletteGroupBar
            // 
            FPaletteGroupBar = new GroupBar();
            FPaletteGroupBar.AllowDrop = true;
            FPaletteGroupBar.BackColor = SystemColors.Control;
            FPaletteGroupBar.BorderStyle = BorderStyle.FixedSingle;
            FPaletteGroupBar.Dock = DockStyle.Fill;
            FPaletteGroupBar.Location = new Point(0, 24);
            FPaletteGroupBar.Name = "FPaletteGroupBar";
            FPaletteGroupBar.SelectedItem = 0;
            FPaletteGroupBar.Size = new Size(163, 163);
            FPaletteGroupBar.TabIndex = 1;
            // 
            // FPointerGroupView
            // 
            FPointerGroupView = new GroupView
                                    {
                                        BorderStyle = BorderStyle.None,
                                        ButtonView = true,
                                        Dock = DockStyle.Top
                                    };
            FPointerGroupView.GroupViewItems.AddRange(new[]
                                                          {
                                                              new GroupViewItem("Pointer", 0)
                                                          });
            FPointerGroupView.IntegratedScrolling = true;
            FPointerGroupView.ItemYSpacing = 2;
            FPointerGroupView.LargeImageList = null;
            FPointerGroupView.Location = new Point(0, 0);
            FPointerGroupView.Name = "FPointerGroupView";
            FPointerGroupView.SelectedItem = 0;
            FPointerGroupView.Size = new Size(163, 24);
            FPointerGroupView.SmallImageList = FPointerImageList;
            FPointerGroupView.SmallImageView = true;
            FPointerGroupView.TabIndex = 0;
            FPointerGroupView.Text = "groupView2";
            FPointerGroupView.GroupViewItemSelected += FPointerGroupView_GroupViewItemSelected;

            Controls.Add(FPaletteGroupBar);
            Controls.Add(FPointerGroupView);
        }

        internal void ClearPalette()
        {
            FPaletteGroupBar.GroupBarItems.Clear();
        }

        private GroupView EnsureCategory(string ACategoryName)
        {
            GroupBarItem LItem = FindPaletteBarItem(ACategoryName);
            if (LItem == null)
            {
                var LView = new GroupView();
                LView.BorderStyle = BorderStyle.None;
                LView.IntegratedScrolling = false;
                LView.ItemYSpacing = 2;
                //LView.SmallImageList = FNodesImageList;
                LView.SmallImageView = true;
                LView.SelectedTextColor = Color.Navy;
                LView.GroupViewItemSelected += new EventHandler(CategoryGroupViewItemSelected);

                LItem = new GroupBarItem();
                LItem.Client = LView;
                LItem.Text = ACategoryName;
                FPaletteGroupBar.GroupBarItems.Add(LItem);
            }
            return (GroupView) LItem.Client;
        }

        internal void LoadPalette()
        {
            PaletteItem LItem;
            Type LType;

            foreach (String LName in FrontendSession.NodeTypeTable.Keys)
            {
                LType = FrontendSession.NodeTypeTable.GetClassType(LName);

                if (IsTypeListed(LType))
                {
                    LItem = new PaletteItem();
                    LItem.ClassName = LType.Name;
                    LItem.Text = LType.Name;
                    LItem.Description = GetDescription(LType);
                    LItem.ImageIndex = GetDesignerImage(LType);
                    GroupView LCategory = EnsureCategory(GetDesignerCategory(LType));
                    LCategory.GroupViewItems.Add(LItem);
                }
            }
        }

        public void SelectPaletteItem(PaletteItem AItem, bool AIsMultiDrop)
        {
            if (AItem != FSelectedPaletteItem)
            {
                FIsMultiDrop = AIsMultiDrop && (AItem != null);

                if (FSelectedPaletteItem != null)
                {
                    FSelectedPaletteItem.GroupView.ButtonView = false;
                    FSelectedPaletteItem.GroupView.SelectedTextColor = Color.Navy;
                }

                FSelectedPaletteItem = AItem;

                if (FSelectedPaletteItem != null)
                {
                    FSelectedPaletteItem.GroupView.ButtonView = true;
                    FSelectedPaletteItem.GroupView.SelectedItem =
                        FSelectedPaletteItem.GroupView.GroupViewItems.IndexOf(FSelectedPaletteItem);

                    if (FIsMultiDrop)
                        FSelectedPaletteItem.GroupView.SelectedTextColor = Color.Blue;

                    FNodesTree.PaletteItem = FSelectedPaletteItem;
                    SetStatus(FSelectedPaletteItem.Description);
                    FPointerGroupView.ButtonView = false;
                }
                else
                {
                    FNodesTree.PaletteItem = null;
                    SetStatus(String.Empty);
                    FPointerGroupView.ButtonView = true;
                }

                FNodesTree.Select();
            }
        }
       

        public void PaletteItemDropped()
        {
            if (!IsMultiDrop)
                SelectPaletteItem(null, false);
        }

        private GroupBarItem FindPaletteBarItem(string AText)
        {
            foreach (GroupBarItem LItem in FPaletteGroupBar.GroupBarItems)
            {
                if (String.Compare(LItem.Text, AText, true) == 0)
                    return LItem;
            }
            return null;
        }

        protected override bool ProcessDialogKey(Keys AKey)
        {
            if
                (
                ((AKey & Keys.Modifiers) == Keys.None) &&
                ((AKey & Keys.KeyCode) == Keys.Escape) &&
                (FSelectedPaletteItem != null)
                )
            {
                SelectPaletteItem(null, false);
                return true;
            }
            return base.ProcessDialogKey(AKey);
        }

        private void FPointerGroupView_GroupViewItemSelected(object ASender, EventArgs AArgs)
        {
            SelectPaletteItem(null, false);
        }

        private void CategoryGroupViewItemSelected(object ASender, EventArgs AArgs)
        {
            var LView = (GroupView) ASender;
            SelectPaletteItem
                (
                (PaletteItem) LView.GroupViewItems[LView.SelectedItem],
                ModifierKeys == Keys.Shift
                );
        }

        #region Palette

        private readonly Hashtable FImageIndex = new Hashtable();
        private bool FIsMultiDrop;
        protected DesignerTree FNodesTree;
        private PaletteItem FSelectedPaletteItem;

        [Browsable(false)]
        public PaletteItem SelectedPaletteItem
        {
            get { return FSelectedPaletteItem; }
        }

        [Browsable(false)]
        public bool IsMultiDrop
        {
            get { return FIsMultiDrop; }
        }
        


        private bool IsTypeListed(Type AType)
        {
            var LListIn =
                (ListInDesignerAttribute) ReflectionUtility.GetAttribute(AType, typeof (ListInDesignerAttribute));
            if (LListIn != null)
                return LListIn.IsListed;
            return true;
        }

        private string GetDescription(Type AType)
        {
            var LDescription =
                (DescriptionAttribute) ReflectionUtility.GetAttribute(AType, typeof (DescriptionAttribute));
            if (LDescription != null)
                return LDescription.Description;
            return String.Empty;
        }

        private string GetDesignerCategory(Type AType)
        {
            var LCategory =
                (DesignerCategoryAttribute) ReflectionUtility.GetAttribute(AType, typeof (DesignerCategoryAttribute));
            if (LCategory != null)
                return LCategory.Category;
            return Strings.UnspecifiedCategory;
        }

        private Image LoadImage(string AImageExpression)
        {
            try
            {
                using (DataValue LImageData = FrontendSession.Pipe.RequestDocument(AImageExpression))
                {
                    var LStreamCopy = new MemoryStream();
                    Stream LStream = LImageData.OpenStream();
                    try
                    {
                        StreamUtility.CopyStream(LStream, LStreamCopy);
                    }
                    finally
                    {
                        LStream.Close();
                    }
                    return Image.FromStream(LStreamCopy);
                }
            }
            catch (Exception LException)
            {
                Program.DataphoriaInstance.Warnings.AppendError(this, LException, true);
                // Don't rethrow
            }
            return null;
        }

        public int GetDesignerImage(Type AType)
        {
            var LImageAttribute =
                (DesignerImageAttribute) ReflectionUtility.GetAttribute(AType, typeof (DesignerImageAttribute));
            if (LImageAttribute != null)
            {
                object LIndexResult = FImageIndex[LImageAttribute.ImageExpression];
                if (LIndexResult == null)
                {
                    Image LImage = LoadImage(LImageAttribute.ImageExpression);
                    if (LImage != null)
                    {
                        if (LImage is Bitmap)
                            ((Bitmap) LImage).MakeTransparent();
                        FNodesImageList.Images.Add(LImage);
                        int LIndex = FNodesImageList.Images.Count - 1;
                        FImageIndex.Add(LImageAttribute.ImageExpression, LIndex);
                        return LIndex;                        
                    }
                    FImageIndex.Add(LImageAttribute.ImageExpression, 0);
                }
                else
                    return (int) LIndexResult;
            }
            return 0; // Zero is the reserved index for the default image
        }

        #endregion

        #region FrontendSession

        [Browsable(false)]
        public Session FrontendSession { get; set; }

        #endregion
    }
}
