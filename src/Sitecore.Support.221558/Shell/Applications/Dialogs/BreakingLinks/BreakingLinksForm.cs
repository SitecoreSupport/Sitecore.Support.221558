
using System;
using System.Collections.Generic;
using System.IO;
using System.Web.UI;
using Sitecore.Data;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Jobs;
using Sitecore.Links;
using Sitecore.Resources;
using Sitecore.SecurityModel;
using Sitecore.Text;
using Sitecore.Web;
using Sitecore.Web.UI.HtmlControls;
using Sitecore.Web.UI.Pages;
using Sitecore.Web.UI.Sheer;
using Sitecore.Web.UI.WebControls;

namespace Sitecore.Support.Shell.Applications.Dialogs.BreakingLinks
{
  public class BreakingLinksForm : DialogForm
  {
    /// <summary>
		/// Represents a RemoveLinks.
		/// </summary>
		public class RemoveLinks
    {
      #region Fields

      /// <summary>
      /// The list of item IDs to be processed.
      /// </summary>
      private readonly string list;

      #endregion

      #region Constructor

      /// <summary>
      /// Initializes a new instance of the <see cref="RemoveLinks"/> class.
      /// </summary>
      /// <param name="list">
      /// The list of item IDs to be processed.
      /// </param>
      public RemoveLinks([NotNull] string list)
      {
        Assert.ArgumentNotNullOrEmpty(list, "list");

        this.list = list;
      }

      #endregion

      #region Protected methods

      /// <summary>
      /// Fixes this instance.
      /// </summary>
      protected void Remove()
      {
        Job job = Context.Job;
        Assert.IsNotNull(job, "job");

        try
        {
          var ids = new ListString(this.list);

          LinkDatabase linkDatabase = Globals.LinkDatabase;

          foreach (string id in ids)
          {
            this.RemoveItemLinks(job, linkDatabase, id);
          }
        }
        catch (Exception ex)
        {
          job.Status.Failed = true;
          job.Status.Messages.Add(ex.ToString());
        }

        job.Status.State = JobState.Finished;
      }

      #endregion

      #region Private methods

      /// <summary>
      /// Removes the link.
      /// </summary>
      /// <param name="version">
      /// The version.
      /// </param>
      /// <param name="itemLink">
      /// The item link.
      /// </param>
      private static void RemoveLink([NotNull] Item version, [NotNull] ItemLink itemLink)
      {
        Assert.ArgumentNotNull(version, "version");
        Assert.ArgumentNotNull(itemLink, "itemLink");

        Field sourceField = version.Fields[itemLink.SourceFieldID];
        CustomField customField = FieldTypeManager.GetField(sourceField);
        if (customField == null)
        {
          return;
        }

        using (new SecurityDisabler())
        {
          version.Editing.BeginEdit();
          customField.RemoveLink(itemLink);

          // #221558: make sure the unverioned statistics-related fields won't be updated.
          version.Editing.EndEdit(false, false);
        }
      }

      /// <summary>
      /// Removes the item links.
      /// </summary>
      /// <param name="job">
      /// The job object.
      /// </param>
      /// <param name="linkDatabase">
      /// The link database.
      /// </param>
      /// <param name="id">
      /// The item id.
      /// </param>
      private void RemoveItemLinks([NotNull] Job job, [NotNull] LinkDatabase linkDatabase, [NotNull] string id)
      {
        Assert.ArgumentNotNull(job, "job");
        Assert.ArgumentNotNull(linkDatabase, "linkDatabase");
        Assert.ArgumentNotNullOrEmpty(id, "id");

        var data = job.Options.CustomData as Dictionary<string, object>;

        Database contentDatabase = data == null ? Context.ContentDatabase : (Database)data["content_database"];
        Item targetItem = contentDatabase.GetItem(id);
        if (targetItem == null)
        {
          return;
        }

        job.Status.Processed++;

        bool removeCloneLinks = true;
        if (data != null && data.ContainsKey("ignoreclones"))
        {
          removeCloneLinks = data["ignoreclones"] as string != "1";
        }

        this.RemoveItemLinks(linkDatabase, targetItem, removeCloneLinks);
      }

      /// <summary>
      /// Removes the item links.
      /// </summary>
      /// <param name="linkDatabase">The link database.</param>
      /// <param name="targetItem">The target item.</param>
      /// <param name="removeCloneLinks">If set to <c>True</c> links from clone items will be removed.</param>
      protected void RemoveItemLinks([NotNull] LinkDatabase linkDatabase, [NotNull] Item targetItem, bool removeCloneLinks)
      {
        Assert.ArgumentNotNull(linkDatabase, "linkDatabase");
        Assert.ArgumentNotNull(targetItem, "targetItem");

        foreach (Item child in targetItem.Children)
        {
          this.RemoveItemLinks(linkDatabase, child, removeCloneLinks);
        }

        ItemLink[] links = linkDatabase.GetReferrers(targetItem);

        foreach (ItemLink itemLink in links)
        {
          if (!removeCloneLinks && (itemLink.SourceFieldID == FieldIDs.Source || itemLink.SourceFieldID == FieldIDs.SourceItem))
          {
            continue;
          }

          Item sourceItem = itemLink.GetSourceItem();
          if (sourceItem == null || ID.IsNullOrEmpty(itemLink.SourceFieldID))
          {
            continue;
          }

          RemoveLink(sourceItem, itemLink);
        }

        Log.Audit(this, "Remove link: {0}", AuditFormatter.FormatItem(targetItem));
      }

      #endregion
    }

    /// <summary>
    /// Represents a RemoveLinks.
    /// </summary>
    public class Relink
    {
      #region Fields

      /// <summary>
      /// The item to link to.
      /// </summary>
      private Item item;

      /// <summary>
      /// The list of item IDs to process.
      /// </summary>
      private string list;

      #endregion

      #region Constructor

      /// <summary>
      /// Initializes a new instance of the <see cref="Relink"/> class.
      /// </summary>
      /// <param name="list">The list of item IDs to process.</param>
      /// <param name="item">The item to link to.</param>
      public Relink([NotNull] string list, [NotNull] Item item)
      {
        Assert.ArgumentNotNullOrEmpty(list, "list");
        Assert.ArgumentNotNull(item, "item");

        this.list = list;
        this.item = item;
      }

      #endregion

      #region Protected methods

      /// <summary>
      /// Fixes this instance.
      /// </summary>
      protected void RelinkItems()
      {
        Job job = Context.Job;
        Assert.IsNotNull(job, "job");

        try
        {
          var ids = new ListString(this.list);

          LinkDatabase linkDatabase = Globals.LinkDatabase;

          foreach (string id in ids)
          {
            this.RelinkItemLinks(job, linkDatabase, id);
          }
        }
        catch (Exception ex)
        {
          job.Status.Failed = true;
          job.Status.Messages.Add(ex.ToString());
        }

        job.Status.State = JobState.Finished;
      }

      #endregion

      #region Private methods

      /// <summary>
      /// Removes the item links.
      /// </summary>
      /// <param name="job">The job object.</param>
      /// <param name="linkDatabase">The link database.</param>
      /// <param name="id">The item id to process.</param>
      private void RelinkItemLinks([NotNull] Job job, [NotNull] LinkDatabase linkDatabase, [NotNull] string id)
      {
        Assert.ArgumentNotNull(job, "job");
        Assert.ArgumentNotNull(linkDatabase, "linkDatabase");
        Assert.ArgumentNotNullOrEmpty(id, "id");

        Assert.IsNotNull(Context.ContentDatabase, "content database");
        Item targetItem = Context.ContentDatabase.GetItem(id);
        if (targetItem == null)
        {
          return;
        }

        job.Status.Processed++;

        bool relinkCloneLinks = true;
        if ((job.Options != null) && (job.Options.CustomData != null) && (job.Options.CustomData is Dictionary<string, object>))
        {
          var data = job.Options.CustomData as Dictionary<string, object>;
          if ((data != null) && data.ContainsKey("ignoreclones"))
          {
            relinkCloneLinks = data["ignoreclones"] as string != "1";
          }
        }

        this.RelinkItemLinks(linkDatabase, targetItem, relinkCloneLinks);
      }

      /// <summary>
      /// Relinks the item links.
      /// </summary>
      /// <param name="linkDatabase">The link database.</param>
      /// <param name="targetItem">The item to process.</param>
      /// <param name="relinkCloneLinks">If set to <c>true</c> links from clones will be processed.</param>
      protected void RelinkItemLinks([NotNull] LinkDatabase linkDatabase, [NotNull] Item targetItem, bool relinkCloneLinks)
      {
        Assert.ArgumentNotNull(linkDatabase, "linkDatabase");
        Assert.ArgumentNotNull(targetItem, "targetItem");

        foreach (Item child in targetItem.Children)
        {
          this.RelinkItemLinks(linkDatabase, child, relinkCloneLinks);
        }

        ItemLink[] links = linkDatabase.GetReferrers(targetItem);

        foreach (ItemLink itemLink in links)
        {
          if (!relinkCloneLinks && (itemLink.SourceFieldID == FieldIDs.Source || itemLink.SourceFieldID == FieldIDs.SourceItem))
          {
            continue;
          }

          Item sourceItem = itemLink.GetSourceItem();
          if (sourceItem == null || ID.IsNullOrEmpty(itemLink.SourceFieldID))
          {
            continue;
          }

          this.RelinkLink(sourceItem, itemLink);
        }
      }

      /// <summary>
      /// Removes the link.
      /// </summary>
      /// <param name="sourceItem">The source item.</param>
      /// <param name="itemLink">The item link.</param>
      private void RelinkLink([NotNull] Item sourceItem, [NotNull] ItemLink itemLink)
      {
        Assert.ArgumentNotNull(sourceItem, "sourceItem");
        Assert.ArgumentNotNull(itemLink, "itemLink");

        Field sourceField = sourceItem.Fields[itemLink.SourceFieldID];

        using (new SecurityDisabler())
        {
          sourceItem.Editing.BeginEdit();

          CustomField customField = FieldTypeManager.GetField(sourceField);

          if (customField != null)
          {
            customField.Relink(itemLink, this.item);
            Log.Audit(this, "Relink: {0}, ReferrerItem: {1}", AuditFormatter.FormatItem(this.item), AuditFormatter.FormatItem(sourceItem));
          }

          sourceItem.Editing.EndEdit();
        }
      }

      #endregion
    }

    /// <summary>
    /// Back Button
    /// </summary>
    protected Button BackButton;

    /// <summary>
    /// The error text.
    /// </summary>
    protected Memo ErrorText;

    /// <summary>
    /// The executing page.
    /// </summary>
    protected Border ExecutingPage;

    /// <summary>
    /// The failed page.
    /// </summary>
    protected Border FailedPage;

    /// <summary>
    /// The link.
    /// </summary>
    protected TreeviewEx Link;

    /// <summary>
    /// The relink button.
    /// </summary>
    protected Radiobutton RelinkButton;

    /// <summary>
    /// The remove button.
    /// </summary>
    protected Radiobutton RemoveButton;

    /// <summary>
    /// The select action page.
    /// </summary>
    protected Border SelectActionPage;

    /// <summary>
    /// The select item page.
    /// </summary>
    protected Border SelectItemPage;

    /// <summary>
    /// The broken or removed links count page
    /// </summary>
    protected Border LinksBrokenOrRemovedPage;

    /// <summary>
    /// The broken or removed links count literal
    /// </summary>
    protected Literal LinksBrokenOrRemovedCount;

    /// <summary>
    /// The lead-in text for items to be deleted.
    /// </summary>
    protected Border DeletingItems;

    /// <summary>
    /// Checks the status.
    /// </summary>
    protected void CheckStatus()
    {
      string expr_19 = Context.ClientPage.ServerProperties["handle"] as string;
      Assert.IsNotNullOrEmpty(expr_19, "raw handle");
      Handle handle = Handle.Parse(expr_19);
      if (!handle.IsLocal)
      {
        Context.ClientPage.ClientResponse.Timer("CheckStatus", 500);
        return;
      }
      Job job = JobManager.GetJob(handle);
      if (job.Status.Failed)
      {
        this.ErrorText.Value = StringUtil.StringCollectionToString(job.Status.Messages);
        this.ShowPage("Failed");
        return;
      }
      string value;
      if (job.Status.State == JobState.Running)
      {
        value = Translate.Text("Processed {0} items. ", new object[]
        {
          job.Status.Processed,
          job.Status.Total
        });
      }
      else
      {
        value = Translate.Text("Queued.");
      }
      if (job.IsDone)
      {
        SheerResponse.SetDialogValue("yes");
        SheerResponse.CloseWindow();
        UrlHandle.DisposeHandle(UrlHandle.Get());
        return;
      }
      SheerResponse.SetInnerHtml("Status", value);
      SheerResponse.Timer("CheckStatus", 500);
    }

    /// <summary>
    /// Edits the links.
    /// </summary>
    protected void EditLinks()
    {
      UrlString urlString = ResourceUri.Parse("control:EditLinks").ToUrlString();
      if (WebUtil.GetQueryString("ignoreclones") == "1")
      {
        urlString.Add("ignoreclones", "1");
      }
      UrlHandle expr_3C = new UrlHandle();
      expr_3C["list"] = UrlHandle.Get()["list"];
      expr_3C.Add(urlString);
      SheerResponse.ShowModalDialog(urlString.ToString());
    }

    /// <summary>
    /// Handles a click on the Cancel button.
    /// </summary>
    /// <param name="sender">The event sender object.</param>
    /// <param name="args">The event args.</param>
    /// <remarks>
    /// When the user clicksCancel, the dialog is closed by calling
    /// the <see cref="M:Sitecore.Web.UI.Sheer.ClientResponse.CloseWindow">CloseWindow</see> method.
    /// </remarks>
    protected override void OnCancel(object sender, EventArgs args)
    {
      Assert.ArgumentNotNull(sender, "sender");
      Assert.ArgumentNotNull(args, "args");
      if (!string.IsNullOrEmpty(Context.ClientPage.ServerProperties["handle"] as string))
      {
        Log.Audit(this, "The RemoveLinks job was cancelled by the user. The target item will therefore not be deleted.  Some or all of the referring links have already been removed or updated.", new string[0]);
      }
      SheerResponse.SetDialogValue("no");
      UrlHandle.DisposeHandle(UrlHandle.Get());
      base.OnCancel(sender, args);
    }

    /// <summary>
    /// Handles a click on the Cancel button.
    /// </summary>
    /// <param name="sender">The event sender object.</param>
    /// <param name="args">The event args.</param>
    /// <remarks>
    /// When the user clicksCancel, the dialog is closed by calling
    /// the <see cref="M:Sitecore.Web.UI.Sheer.ClientResponse.CloseWindow">CloseWindow</see> method.
    /// </remarks>
    protected void OnBackButton(object sender, EventArgs args)
    {
      Assert.ArgumentNotNull(sender, "sender");
      Assert.ArgumentNotNull(args, "args");
      this.ShowPage("Action");
    }

    /// <summary>
    /// Raises the load event.
    /// </summary>
    /// <param name="e">
    /// The <see cref="T:System.EventArgs" /> instance containing the event data.
    /// </param>
    /// <remarks>
    /// This method notifies the server control that it should perform actions common to each HTTP
    /// request for the page it is associated with, such as setting up a database query. At this
    /// stage in the page lifecycle, server controls in the hierarchy are created and initialized,
    /// view state is restored, and form controls reflect client-side data. Use the IsPostBack
    /// property to determine whether the page is being loaded in response to a client postback,
    /// or if it is being loaded and accessed for the first time.
    /// </remarks>
    protected override void OnLoad(EventArgs e)
    {
      Assert.ArgumentNotNull(e, "e");
      base.OnLoad(e);
      if (this.BackButton != null)
      {
        this.BackButton.OnClick += new EventHandler(this.OnBackButton);
      }
      if (!Context.ClientPage.IsEvent)
      {
        this.BuildItemsToBeDeleted();
      }
    }

    /// <summary>
    /// Handles a click on the OK button.
    /// </summary>
    /// <param name="sender">The event sender object.</param>
    /// <param name="args">The event args.</param>
    /// <remarks>
    /// When the user clicks OK, the dialog is closed by calling
    /// the <see cref="M:Sitecore.Web.UI.Sheer.ClientResponse.CloseWindow">CloseWindow</see> method.
    /// </remarks>
    /// <exception cref="T:System.InvalidOperationException">Unknown action</exception>
    protected override void OnOK(object sender, EventArgs args)
    {
      Assert.ArgumentNotNull(sender, "sender");
      Assert.ArgumentNotNull(args, "args");
      string formValue = WebUtil.GetFormValue("Action");
      if (formValue == "Remove")
      {
        if (this.SelectActionPage.Visible)
        {
          this.ShowLinksBrokenOrRemovedCount();
          return;
        }
        this.StartRemove();
        return;
      }
      else if (formValue == "Relink")
      {
        if (this.SelectActionPage.Visible)
        {
          this.SelectItem();
          return;
        }
        this.StartRelink();
        return;
      }
      else
      {
        if (!(formValue == "Break"))
        {
          throw new InvalidOperationException(string.Format("Unknown action: '{0}'", formValue));
        }
        if (this.SelectActionPage.Visible)
        {
          this.ShowLinksBrokenOrRemovedCount();
          return;
        }
        SheerResponse.SetDialogValue("yes");
        base.OnOK(sender, args);
        UrlHandle.DisposeHandle(UrlHandle.Get());
        return;
      }
    }

    /// <summary>
    /// Starts the remove.
    /// </summary>
    private void SelectItem()
    {
      this.ShowPage("Item");
    }

    /// <summary>
    /// Shows the page.
    /// </summary>
    /// <param name="pageID">
    /// The page ID.
    /// </param>
    private void ShowPage(string pageID)
    {
      Assert.ArgumentNotNullOrEmpty(pageID, "pageID");
      this.SelectActionPage.Visible = (pageID == "Action");
      this.SelectItemPage.Visible = (pageID == "Item");
      this.ExecutingPage.Visible = (pageID == "Executing");
      this.FailedPage.Visible = (pageID == "Failed");
      this.LinksBrokenOrRemovedPage.Visible = (pageID == "LinksBrokenOrRemoved");
      this.BackButton.Visible = (pageID != "Action" && pageID != "Executing");
      this.OK.Visible = (pageID != "Executing");
    }

    /// <summary>
    /// Returns number of referrers to item (and all of its children)
    /// </summary>
    /// <param name="item">The item</param>
    private int CountReferrers(Item item)
    {
      int num = Globals.LinkDatabase.GetReferrerCount(item);
      foreach (Item item2 in item.Children)
      {
        num += this.CountReferrers(item2);
      }
      return num;
    }

    /// <summary>
    /// Starts the remove.
    /// </summary>
    /// <exception cref="T:System.InvalidOperationException">Unknown action</exception>
    private void ShowLinksBrokenOrRemovedCount()
    {
      Assert.IsNotNull(Context.ContentDatabase, "content database");
      int num = 0;
      foreach (string current in new ListString(UrlHandle.Get()["list"]))
      {
        Item item = Context.ContentDatabase.GetItem(current);
        Assert.IsNotNull(item, "item");
        num += this.CountReferrers(item);
      }
      string formValue = WebUtil.GetFormValue("Action");
      if (formValue == "Remove")
      {
        this.LinksBrokenOrRemovedCount.Text = string.Format(Translate.Text("If you delete this item, you will permanently remove every link to it. Number of links to this item: {0}"), num);
      }
      else
      {
        if (!(formValue == "Break"))
        {
          throw new InvalidOperationException(string.Format("Invalid action: '{0}'", formValue));
        }
        this.LinksBrokenOrRemovedCount.Text = string.Format(Translate.Text("If you delete this item, you will leave broken links. Number of links to this item: {0}"), num);
      }
      this.ShowPage("LinksBrokenOrRemoved");
    }

    /// <summary>
    /// Starts the remove.
    /// </summary>
    private void StartRelink()
    {
      string list = UrlHandle.Get()["list"];
      Item selectionItem = this.Link.GetSelectionItem();
      if (selectionItem == null)
      {
        SheerResponse.Alert("Select an item.", new string[0]);
        return;
      }
      this.ShowPage("Executing");
      Dictionary<string, object> dictionary = new Dictionary<string, object>();
      if (WebUtil.GetQueryString("ignoreclones") == "1")
      {
        dictionary.Add("ignoreclones", "1");
      }
      Job job = JobManager.Start(new JobOptions("Relink", "Relink", Client.Site.Name, new BreakingLinksForm.Relink(list, selectionItem), "RelinkItems")
      {
        AfterLife = TimeSpan.FromMinutes(1.0),
        ContextUser = Context.User,
        CustomData = dictionary
      });
      Context.ClientPage.ServerProperties["handle"] = job.Handle.ToString();
      Context.ClientPage.ClientResponse.Timer("CheckStatus", 500);
    }

    /// <summary>
    /// Starts the remove.
    /// </summary>
    private void StartRemove()
    {
      string list = UrlHandle.Get()["list"];
      this.ShowPage("Executing");
      Dictionary<string, object> dictionary = new Dictionary<string, object>();
      if (WebUtil.GetQueryString("ignoreclones") == "1")
      {
        dictionary.Add("ignoreclones", "1");
      }
      dictionary["content_database"] = Context.ContentDatabase;
      Job job = JobManager.Start(new JobOptions("RemoveLinks", "RemoveLinks", Client.Site.Name, new BreakingLinksForm.RemoveLinks(list), "Remove")
      {
        AfterLife = TimeSpan.FromMinutes(1.0),
        ContextUser = Context.User,
        CustomData = dictionary
      });
      Context.ClientPage.ServerProperties["handle"] = job.Handle.ToString();
      Context.ClientPage.ClientResponse.Timer("CheckStatus", 500);
    }

    /// <summary>
    /// Show the list of items to be deleted
    /// </summary>
    private void BuildItemsToBeDeleted()
    {
      Assert.IsNotNull(Context.ContentDatabase, "content database");
      HtmlTextWriter htmlTextWriter = new HtmlTextWriter(new StringWriter());
      foreach (string current in new ListString(UrlHandle.Get()["list"]))
      {
        Item item = Context.ContentDatabase.GetItem(current);
        if (item != null)
        {
          htmlTextWriter.Write("<div>");
          htmlTextWriter.Write("<table class=\"scLinkTable\" cellpadding=\"0\" cellspacing=\"0\"><tr>");
          htmlTextWriter.Write("<td>");
          ImageBuilder imageBuilder = new ImageBuilder
          {
            Src = item.Appearance.Icon,
            Width = 32,
            Height = 32,
            Class = "scLinkIcon"
          };
          htmlTextWriter.Write(imageBuilder.ToString());
          htmlTextWriter.Write("</td>");
          htmlTextWriter.Write("<td>");
          htmlTextWriter.Write("<div class=\"scLinkHeader\">");
          htmlTextWriter.Write(item.GetUIDisplayName());
          htmlTextWriter.Write("</div>");
          htmlTextWriter.Write("<div class=\"scLinkDetails\">");
          htmlTextWriter.Write(item.Paths.ContentPath);
          htmlTextWriter.Write("</div>");
          htmlTextWriter.Write("</td>");
          htmlTextWriter.Write("</tr></table>");
          htmlTextWriter.Write("</div>");
        }
      }
      this.DeletingItems.InnerHtml = htmlTextWriter.InnerWriter.ToString();
    }
  }
}