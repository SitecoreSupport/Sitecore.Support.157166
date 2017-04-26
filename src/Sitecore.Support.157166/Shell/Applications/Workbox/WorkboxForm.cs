namespace Sitecore.Support.Shell.Applications.Workbox
{
    using Sitecore;
    using Sitecore.Collections;
    using Sitecore.Configuration;
    using Sitecore.Data;
    using Sitecore.Data.Items;
    using Sitecore.Diagnostics;
    using Sitecore.Exceptions;
    using Sitecore.Globalization;
    using Sitecore.Pipelines;
    using Sitecore.Pipelines.GetWorkflowCommentsDisplay;
    using Sitecore.Resources;
    using Sitecore.Shell.Data;
    using Sitecore.Shell.Feeds;
    using Sitecore.Shell.Framework;
    using Sitecore.Shell.Framework.CommandBuilders;
    using Sitecore.Shell.Framework.Commands;
    using Sitecore.Text;
    using Sitecore.Web;
    using Sitecore.Web.UI.HtmlControls;
    using Sitecore.Web.UI.Sheer;
    using Sitecore.Web.UI.WebControls.Ribbons;
    using Sitecore.Web.UI.XmlControls;
    using Sitecore.Workflows;
    using Sitecore.Workflows.Simple;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Web;
    using System.Web.UI;

    public class WorkboxForm : BaseForm
    {
        private readonly int CommentMaxLength = 0x7d0;
        private OffsetCollection Offset = new OffsetCollection();
        protected Border Pager;
        protected Border RibbonPanel;
        private NameValueCollection stateNames;
        protected Border States;
        protected Toolmenubutton ViewMenu;

        public void Comment(ClientPipelineArgs args)
        {
            Assert.ArgumentNotNull(args, "args");
            object obj2 = Context.ClientPage.ServerProperties["items"];
            Assert.IsNotNull(obj2, "Items is null");
            List<ItemUri> itemUris = (List<ItemUri>)obj2;
            ID @null = ID.Null;
            if (Context.ClientPage.ServerProperties["command"] != null)
            {
                ID.TryParse(Context.ClientPage.ServerProperties["command"] as string, out @null);
            }
            bool flag = ((args.Parameters["ui"] != null) && (args.Parameters["ui"] == "1")) || ((args.Parameters["suppresscomment"] != null) && (args.Parameters["suppresscomment"] == "1"));
            if ((!args.IsPostBack && !(@null.IsNull)) && !flag)
            {
                this.DisplayCommentDialog(itemUris, @null, args);
            }
            else if ((((args.Result != null) && (args.Result != "null")) && ((args.Result != "undefined") && (args.Result != "cancel"))) || flag)
            {
                string result = args.Result;
                Sitecore.Collections.StringDictionary fields = new Sitecore.Collections.StringDictionary();
                string workflowStateId = string.Empty;
                if (Context.ClientPage.ServerProperties["workflowStateid"] != null)
                {
                    workflowStateId = Context.ClientPage.ServerProperties["workflowStateid"].ToString();
                }
                string command = Context.ClientPage.ServerProperties["command"].ToString();
                IWorkflow workflowFromPage = this.GetWorkflowFromPage();
                if (workflowFromPage != null)
                {
                    if (!string.IsNullOrEmpty(result))
                    {
                        fields = WorkflowUIHelper.ExtractFieldsFromFieldEditor(result);
                    }
                    if (!string.IsNullOrWhiteSpace(fields["Comments"]) && (fields["Comments"].Length > this.CommentMaxLength))
                    {
                        Context.ClientPage.ClientResponse.Alert(string.Format("The comment is too long.\n\nYou have entered {0} characters.\nA comment cannot contain more than 2000 characters.", fields["Comments"].Length));
                        this.DisplayCommentDialog(itemUris, @null, args);
                    }
                    else
                    {
                        this.ExecutCommand(itemUris, workflowFromPage, fields, command, workflowStateId);
                        this.Refresh();
                    }
                }
            }
        }

        private void CreateCommand(IWorkflow workflow, WorkflowCommand command, Item item, XmlControl workboxItem)
        {
            Assert.ArgumentNotNull(workflow, "workflow");
            Assert.ArgumentNotNull(command, "command");
            Assert.ArgumentNotNull(item, "item");
            Assert.ArgumentNotNull(workboxItem, "workboxItem");
            XmlControl webControl = Resource.GetWebControl("WorkboxCommand") as XmlControl;
            Assert.IsNotNull(webControl, "workboxCommand is null");
            webControl["Header"] = command.DisplayName;
            webControl["Icon"] = command.Icon;
            CommandBuilder builder = new CommandBuilder("workflow:send");
            builder.Add("id", item.ID.ToString());
            builder.Add("la", item.Language.Name);
            builder.Add("vs", item.Version.ToString());
            builder.Add("command", command.CommandID);
            builder.Add("wf", workflow.WorkflowID);
            builder.Add("ui", command.HasUI);
            builder.Add("suppresscomment", command.SuppressComment);
            webControl["Command"] = builder.ToString();
            workboxItem.AddControl(webControl);
        }

        private void CreateItem(IWorkflow workflow, Item item, System.Web.UI.Control control)
        {
            Assert.ArgumentNotNull(workflow, "workflow");
            Assert.ArgumentNotNull(item, "item");
            Assert.ArgumentNotNull(control, "control");
            XmlControl webControl = Resource.GetWebControl("WorkboxItem") as XmlControl;
            Assert.IsNotNull(webControl, "workboxItem is null");
            control.Controls.Add(webControl);
            StringBuilder builder = new StringBuilder(" - (");
            Language language = item.Language;
            builder.Append(language.CultureInfo.DisplayName);
            builder.Append(", ");
            builder.Append(Translate.Text("version"));
            builder.Append(' ');
            builder.Append(item.Version.ToString());
            builder.Append(")");
            Assert.IsNotNull(webControl, "workboxItem");
            WorkflowEvent[] history = workflow.GetHistory(item);
            webControl["Header"] = item.GetUIDisplayName();
            webControl["Details"] = builder.ToString();
            webControl["Icon"] = item.Appearance.Icon;
            webControl["ShortDescription"] = Settings.ContentEditor.RenderItemHelpAsHtml ? WebUtil.RemoveAllScripts(item.Help.ToolTip) : System.Web.HttpUtility.HtmlEncode(item.Help.ToolTip);
            webControl["History"] = this.GetHistory(workflow, history);
            webControl["LastComments"] = System.Web.HttpUtility.HtmlEncode(this.GetLastComments(history, item));
            webControl["HistoryMoreID"] = Sitecore.Web.UI.HtmlControls.Control.GetUniqueID("ctl");
            webControl["HistoryClick"] = string.Concat(new object[] { "workflow:showhistory(id=", item.ID, ",la=", item.Language.Name, ",vs=", item.Version, ",wf=", workflow.WorkflowID, ")" });
            webControl["PreviewClick"] = string.Concat(new object[] { "Preview(\"", item.ID, "\", \"", item.Language, "\", \"", item.Version, "\")" });
            webControl["Click"] = string.Concat(new object[] { "Open(\"", item.ID, "\", \"", item.Language, "\", \"", item.Version, "\")" });
            webControl["DiffClick"] = string.Concat(new object[] { "Diff(\"", item.ID, "\", \"", item.Language, "\", \"", item.Version, "\")" });
            webControl["Display"] = "none";
            string uniqueID = Sitecore.Web.UI.HtmlControls.Control.GetUniqueID(string.Empty);
            webControl["CheckID"] = "check_" + uniqueID;
            webControl["HiddenID"] = "hidden_" + uniqueID;
            webControl["CheckValue"] = string.Concat(new object[] { item.ID, ",", item.Language, ",", item.Version });
            foreach (WorkflowCommand command in WorkflowFilterer.FilterVisibleCommands(workflow.GetCommands(item), item))
            {
                this.CreateCommand(workflow, command, item, webControl);
            }
        }

        private void CreateNavigator(Sitecore.Web.UI.HtmlControls.Section section, string id, int count, int offset)
        {
            Assert.ArgumentNotNull(section, "section");
            Assert.ArgumentNotNull(id, "id");
            Navigator child = new Navigator();
            section.Controls.Add(child);
            child.ID = id;
            child.Offset = offset;
            child.Count = count;
            child.PageSize = this.PageSize;
        }

        protected void Diff(string id, string language, string version)
        {
            Assert.ArgumentNotNull(id, "id");
            Assert.ArgumentNotNull(language, "language");
            Assert.ArgumentNotNull(version, "version");
            UrlString str = new UrlString(UIUtil.GetUri("control:Diff"));
            str.Append("id", id);
            str.Append("la", language);
            str.Append("vs", version);
            str.Append("wb", "1");
            Context.ClientPage.ClientResponse.ShowModalDialog(str.ToString());
        }

        protected virtual void DisplayCommentDialog(List<ItemUri> itemUris, ID commandId, ClientPipelineArgs args)
        {
            WorkflowUIHelper.DisplayCommentDialog(itemUris, commandId);
            args.WaitForPostBack();
        }

        protected virtual void DisplayState(IWorkflow workflow, WorkflowState state, StateItems stateItems, System.Web.UI.Control control, int offset, int pageSize)
        {
            Assert.ArgumentNotNull(workflow, "workflow");
            Assert.ArgumentNotNull(state, "state");
            Assert.ArgumentNotNull(stateItems, "stateItems");
            Assert.ArgumentNotNull(control, "control");
            Item[] itemArray = stateItems.Items.ToArray<Item>();
            if (itemArray.Length > 0)
            {
                int length = offset + pageSize;
                if (length > itemArray.Length)
                {
                    length = itemArray.Length;
                }
                for (int i = offset; i < length; i++)
                {
                    this.CreateItem(workflow, itemArray[i], control);
                }
                Border child = new Border
                {
                    Background = "#fff"
                };
                control.Controls.Add(child);
                child.Margin = "0 5px 10px 15px";
                child.Padding = "5px 10px";
                child.Class = "scWorkboxToolbarButtons";
                foreach (WorkflowCommand command in workflow.GetCommands(state.StateID))
                {
                    if (stateItems.CommandIds.Contains<string>(command.CommandID))
                    {
                        XmlControl webControl = Resource.GetWebControl("WorkboxCommand") as XmlControl;
                        Assert.IsNotNull(webControl, "workboxCommand is null");
                        webControl["Header"] = command.DisplayName + " " + Translate.Text("(selected)");
                        webControl["Icon"] = command.Icon;
                        webControl["Command"] = "workflow:sendselected(command=" + command.CommandID + ",ws=" + state.StateID + ",wf=" + workflow.WorkflowID + ")";
                        child.Controls.Add(webControl);
                        webControl = Resource.GetWebControl("WorkboxCommand") as XmlControl;
                        Assert.IsNotNull(webControl, "workboxCommand is null");
                        webControl["Header"] = command.DisplayName + " " + Translate.Text("(all)");
                        webControl["Icon"] = command.Icon;
                        webControl["Command"] = "workflow:sendall(command=" + command.CommandID + ",ws=" + state.StateID + ",wf=" + workflow.WorkflowID + ")";
                        child.Controls.Add(webControl);
                    }
                }
            }
        }

        protected virtual void DisplayStates(IWorkflow workflow, XmlControl placeholder)
        {
            Assert.ArgumentNotNull(workflow, "workflow");
            Assert.ArgumentNotNull(placeholder, "placeholder");
            this.stateNames = null;
            foreach (WorkflowState state in workflow.GetStates())
            {
                StateItems stateItems = this.GetStateItems(state, workflow);
                Assert.IsNotNull(stateItems, "stateItems is null");
                if (stateItems.CommandIds.Any<string>())
                {
                    string str2;
                    string str = ShortID.Encode(workflow.WorkflowID) + "_" + ShortID.Encode(state.StateID);
                    Sitecore.Web.UI.HtmlControls.Section control = new Sitecore.Web.UI.HtmlControls.Section
                    {
                        ID = str + "_section"
                    };
                    placeholder.AddControl(control);
                    int num = stateItems.Items.Count<Item>();
                    if (num <= 0)
                    {
                        str2 = Translate.Text("None");
                    }
                    else if (num == 1)
                    {
                        str2 = string.Format("1 {0}", Translate.Text("item"));
                    }
                    else
                    {
                        str2 = string.Format("{0} {1}", num, Translate.Text("items"));
                    }
                    str2 = string.Format("<span style=\"font-weight:normal\"> - ({0})</span>", str2);
                    control.Header = state.DisplayName + str2;
                    control.Icon = state.Icon;
                    if (Settings.ClientFeeds.Enabled)
                    {
                        FeedUrlOptions options = new FeedUrlOptions("/sitecore/shell/~/feed/workflowstate.aspx")
                        {
                            UseUrlAuthentication = true
                        };
                        options.Parameters["wf"] = workflow.WorkflowID;
                        options.Parameters["st"] = state.StateID;
                        control.FeedLink = options.ToString();
                    }
                    control.Collapsed = num <= 0;
                    Border child = new Border();
                    control.Controls.Add(child);
                    child.ID = str + "_content";
                    this.DisplayState(workflow, state, stateItems, child, this.Offset[state.StateID], this.PageSize);
                    this.CreateNavigator(control, str + "_navigator", num, this.Offset[state.StateID]);
                }
            }
        }

        protected virtual void DisplayWorkflow(IWorkflow workflow)
        {
            Assert.ArgumentNotNull(workflow, "workflow");
            Context.ClientPage.ServerProperties["WorkflowID"] = workflow.WorkflowID;
            XmlControl webControl = Resource.GetWebControl("Pane") as XmlControl;
            Error.AssertXmlControl(webControl, "Pane");
            this.States.Controls.Add(webControl);
            Assert.IsNotNull(webControl, "pane");
            webControl["PaneID"] = this.GetPaneID(workflow);
            webControl["Header"] = workflow.Appearance.DisplayName;
            webControl["Icon"] = workflow.Appearance.Icon;
            FeedUrlOptions options = new FeedUrlOptions("/sitecore/shell/~/feed/workflow.aspx")
            {
                UseUrlAuthentication = true
            };
            options.Parameters["wf"] = workflow.WorkflowID;
            webControl["FeedLink"] = options.ToString();
            this.DisplayStates(workflow, webControl);
            if (Context.ClientPage.IsEvent)
            {
                SheerResponse.Insert(this.States.ClientID, "append", HtmlUtil.RenderControl(webControl));
            }
        }

        protected virtual void ExecutCommand(List<ItemUri> itemUris, IWorkflow workflow, Sitecore.Collections.StringDictionary fields, string command, string workflowStateId)
        {
            bool flag = false;
            if (fields == null)
            {
                fields = new Sitecore.Collections.StringDictionary();
            }
            foreach (ItemUri uri in itemUris)
            {
                Item item = Context.ContentDatabase.GetItem(uri.ItemID);
                if (item == null)
                {
                    flag = true;
                }
                else
                {
                    WorkflowState state = workflow.GetState(item);
                    if ((state != null) && (string.IsNullOrWhiteSpace(workflowStateId) || (state.StateID == workflowStateId)))
                    {
                        if ((fields.Count < 1) || !fields.ContainsKey("Comments"))
                        {
                            string str = string.IsNullOrWhiteSpace(state.DisplayName) ? string.Empty : state.DisplayName;
                            fields.Add("Comments", str);
                        }
                        try
                        {
                            if (itemUris.Count == 1)
                            {
                                Processor completionCallback = new Processor("Workflow complete state item count", this, "WorkflowCompleteStateItemCount");
                                workflow.Execute(command, item, fields, true, completionCallback, new object[0]);
                            }
                            else
                            {
                                workflow.Execute(command, item, fields, true, new object[0]);
                            }
                        }
                        catch (WorkflowStateMissingException)
                        {
                            flag = true;
                        }
                    }
                }
            }
            if (flag)
            {
                SheerResponse.Alert("One or more items could not be processed because their workflow state does not specify the next step.", new string[0]);
            }
        }

        private string GetHistory(IWorkflow workflow, WorkflowEvent[] events)
        {
            Assert.ArgumentNotNull(workflow, "workflow");
            Assert.ArgumentNotNull(events, "events");
            if (events.Length > 0)
            {
                WorkflowEvent event2 = events[events.Length - 1];
                string user = event2.User;
                string name = Context.Domain.Name;
                if (user.StartsWith(name + @"\", StringComparison.OrdinalIgnoreCase))
                {
                    user = StringUtil.Mid(user, name.Length + 1);
                }
                user = StringUtil.GetString(new string[] { user, Translate.Text("Unknown") });
                string stateName = this.GetStateName(workflow, event2.OldState);
                string str5 = this.GetStateName(workflow, event2.NewState);
                return string.Format(Translate.Text("{0} changed from <b>{1}</b> to <b>{2}</b> on {3}."), new object[] { user, stateName, str5, DateUtil.FormatDateTime(DateUtil.ToServerTime(event2.Date), "D", Context.User.Profile.Culture) });
            }
            return Translate.Text("No changes have been made.");
        }

        protected virtual DataUri[] GetItems(WorkflowState state, IWorkflow workflow)
        {
            Assert.ArgumentNotNull(state, "state");
            Assert.ArgumentNotNull(workflow, "workflow");
            Assert.Required(Context.ContentDatabase, "Context.ContentDatabase");
            DataUri[] items = workflow.GetItems(state.StateID);
            if ((items == null) || (items.Length == 0))
            {
                return new DataUri[0];
            }
            ArrayList list = new ArrayList(items.Length);
            foreach (DataUri uri in items)
            {
                Item item = Context.ContentDatabase.GetItem(uri);
                if ((((item != null) && item.Access.CanRead()) && (item.Access.CanReadLanguage() && item.Access.CanWriteLanguage())) && ((Context.IsAdministrator || item.Locking.CanLock()) || item.Locking.HasLock()))
                {
                    list.Add(uri);
                }
            }
            return (list.ToArray(typeof(DataUri)) as DataUri[]);
        }

        private string GetLastComments(WorkflowEvent[] events, Item item)
        {
            Assert.ArgumentNotNull(events, "events");
            if (events.Length > 0)
            {
                WorkflowEvent workflowEvent = events[events.Length - 1];
                return GetWorkflowCommentsDisplayPipeline.Run(workflowEvent, item);
            }
            return string.Empty;
        }

        private string GetPaneID(IWorkflow workflow)
        {
            Assert.ArgumentNotNull(workflow, "workflow");
            return ("P" + Regex.Replace(workflow.WorkflowID, @"\W", string.Empty));
        }

        private StateItems GetStateItems(WorkflowState state, IWorkflow workflow)
        {
            Assert.ArgumentNotNull(state, "state");
            Assert.ArgumentNotNull(workflow, "workflow");
            List<Item> list = new List<Item>();
            List<string> list2 = new List<string>();
            DataUri[] items = workflow.GetItems(state.StateID);
            bool flag = items.Length > Settings.Workbox.StateCommandFilteringItemThreshold;
            if (items != null)
            {
                foreach (DataUri uri in items)
                {
                    Item item = Context.ContentDatabase.GetItem(uri);
                    if ((((item != null) && item.Access.CanRead()) && (item.Access.CanReadLanguage() && item.Access.CanWriteLanguage())) && ((Context.IsAdministrator || item.Locking.CanLock()) || item.Locking.HasLock()))
                    {
                        list.Add(item);
                        if (!flag)
                        {
                            foreach (WorkflowCommand command in WorkflowFilterer.FilterVisibleCommands(workflow.GetCommands(item), item))
                            {
                                if (!list2.Contains(command.CommandID))
                                {
                                    list2.Add(command.CommandID);
                                }
                            }
                        }
                    }
                }
            }
            if (flag)
            {
                WorkflowCommand[] commandArray2 = WorkflowFilterer.FilterVisibleCommands(workflow.GetCommands(state.StateID));
                list2.AddRange(from x in commandArray2 select x.CommandID);
            }
            return new StateItems
            {
                Items = list,
                CommandIds = list2
            };
        }

        private string GetStateName(IWorkflow workflow, string stateID)
        {
            Assert.ArgumentNotNull(workflow, "workflow");
            Assert.ArgumentNotNull(stateID, "stateID");
            if (this.stateNames == null)
            {
                this.stateNames = new NameValueCollection();
                foreach (WorkflowState state in workflow.GetStates())
                {
                    this.stateNames.Add(state.StateID, state.DisplayName);
                }
            }
            return StringUtil.GetString(new string[] { this.stateNames[stateID], "?" });
        }

        protected virtual IWorkflow GetWorkflowFromPage()
        {
            IWorkflowProvider workflowProvider = Context.ContentDatabase.WorkflowProvider;
            if (workflowProvider == null)
            {
                return null;
            }
            return workflowProvider.GetWorkflow(Context.ClientPage.ServerProperties["WorkflowID"] as string);
        }

        public override void HandleMessage(Message message)
        {
            Assert.ArgumentNotNull(message, "message");
            switch (message.Name)
            {
                case "workflow:send":
                    this.Send(message);
                    return;

                case "workflow:sendselected":
                    this.SendSelected(message);
                    return;

                case "workflow:sendall":
                    this.SendAll(message);
                    return;

                case "window:close":
                    Windows.Close();
                    return;

                case "workflow:showhistory":
                    ShowHistory(message, Context.ClientPage.ClientRequest.Control);
                    return;

                case "workbox:hide":
                    Context.ClientPage.SendMessage(this, "pane:hide(id=" + message["id"] + ")");
                    Context.ClientPage.ClientResponse.SetAttribute("Check_Check_" + message["id"], "checked", "false");
                    break;

                case "pane:hidden":
                    Context.ClientPage.ClientResponse.SetAttribute("Check_Check_" + message["paneid"], "checked", "false");
                    break;

                case "workbox:show":
                    Context.ClientPage.SendMessage(this, "pane:show(id=" + message["id"] + ")");
                    Context.ClientPage.ClientResponse.SetAttribute("Check_Check_" + message["id"], "checked", "true");
                    break;

                case "pane:showed":
                    Context.ClientPage.ClientResponse.SetAttribute("Check_Check_" + message["paneid"], "checked", "true");
                    break;
            }
            base.HandleMessage(message);
            string str = message["id"];
            if (!string.IsNullOrEmpty(str))
            {
                string name = StringUtil.GetString(new string[] { message["language"] });
                string str3 = StringUtil.GetString(new string[] { message["version"] });
                Item item = Context.ContentDatabase.Items[str, Language.Parse(name), Sitecore.Data.Version.Parse(str3)];
                if (item != null)
                {
                    Dispatcher.Dispatch(message, item);
                }
            }
        }

        private void InitializeCommentDialog(List<ItemUri> itemUris, Message message)
        {
            Context.ClientPage.ServerProperties["items"] = itemUris;
            Context.ClientPage.ServerProperties["command"] = message["command"];
            Context.ClientPage.ServerProperties["workflowid"] = message["wf"];
            Context.ClientPage.ServerProperties["workflowStateid"] = message["ws"];
            NameValueCollection parameters = new NameValueCollection();
            parameters.Add("ui", message["ui"]);
            parameters.Add("suppresscomment", message["suppresscomment"]);
            Context.ClientPage.Start(this, "Comment", parameters);
        }

        private void Jump(object sender, Message message, int offset)
        {
            Assert.ArgumentNotNull(sender, "sender");
            Assert.ArgumentNotNull(message, "message");
            string control = Context.ClientPage.ClientRequest.Control;
            string workflowID = ShortID.Decode(control.Substring(0, 0x20));
            string stateID = ShortID.Decode(control.Substring(0x21, 0x20));
            control = control.Substring(0, 0x41);
            this.Offset[stateID] = offset;
            IWorkflowProvider workflowProvider = Context.ContentDatabase.WorkflowProvider;
            Assert.IsNotNull(workflowProvider, "Workflow provider for database \"" + Context.ContentDatabase.Name + "\" not found.");
            IWorkflow workflow = workflowProvider.GetWorkflow(workflowID);
            Error.Assert(workflow != null, "Workflow \"" + workflowID + "\" not found.");
            Assert.IsNotNull(workflow, "workflow");
            WorkflowState state = workflow.GetState(stateID);
            Assert.IsNotNull(state, "Workflow state \"" + stateID + "\" not found.");
            Border border = new Border
            {
                ID = control + "_content"
            };
            StateItems stateItems = this.GetStateItems(state, workflow);
            this.DisplayState(workflow, state, stateItems ?? new StateItems(), border, offset, this.PageSize);
            Context.ClientPage.ClientResponse.SetOuterHtml(control + "_content", border);
        }

        protected override void OnLoad(EventArgs e)
        {
            Assert.ArgumentNotNull(e, "e");
            base.OnLoad(e);
            if (!Context.ClientPage.IsEvent)
            {
                IWorkflowProvider workflowProvider = Context.ContentDatabase.WorkflowProvider;
                if (workflowProvider != null)
                {
                    IWorkflow[] workflows = workflowProvider.GetWorkflows();
                    foreach (IWorkflow workflow in workflows)
                    {
                        string str = "P" + Regex.Replace(workflow.WorkflowID, @"\W", string.Empty);
                        if ((!this.IsReload && (workflows.Length == 1)) && string.IsNullOrEmpty(Registry.GetString("/Current_User/Panes/" + str)))
                        {
                            Registry.SetString("/Current_User/Panes/" + str, "visible");
                        }

                        if ((Registry.GetString("/Current_User/Panes/" + str) ?? string.Empty) == "collapsed")
                        {
                            Registry.SetString("/Current_User/Panes/" + str, "visible");
                        }

                        if ((Registry.GetString("/Current_User/Panes/" + str) ?? string.Empty) == "visible")
                        {
                            this.DisplayWorkflow(workflow);
                        }
                    }
                }
                this.UpdateRibbon();
            }
            this.WireUpNavigators(Context.ClientPage);
        }

        protected void OnViewMenuClick()
        {
            Menu control = new Menu();
            IWorkflowProvider workflowProvider = Context.ContentDatabase.WorkflowProvider;
            if (workflowProvider != null)
            {
                foreach (IWorkflow workflow in workflowProvider.GetWorkflows())
                {
                    string paneID = this.GetPaneID(workflow);
                    string str2 = Registry.GetString("/Current_User/Panes/" + paneID);
                    control.Add(Sitecore.Web.UI.HtmlControls.Control.GetUniqueID("ctl"), workflow.Appearance.DisplayName, workflow.Appearance.Icon, string.Empty, ((str2 != "hidden") ? "workbox:hide" : "workbox:show") + "(id=" + paneID + ")", str2 != "hidden", string.Empty, MenuItemType.Check);
                }
                if (control.Controls.Count > 0)
                {
                    control.AddDivider();
                }
                control.Add("Refresh", "Office/16x16/refresh.png", "Refresh");
            }
            Context.ClientPage.ClientResponse.ShowPopup("ViewMenu", "below", control);
        }

        protected void Open(string id, string language, string version)
        {
            Assert.ArgumentNotNull(id, "id");
            Assert.ArgumentNotNull(language, "language");
            Assert.ArgumentNotNull(version, "version");
            string sectionID = RootSections.GetSectionID(id);
            UrlString str2 = new UrlString();
            str2.Append("ro", sectionID);
            str2.Append("fo", id);
            str2.Append("id", id);
            str2.Append("la", language);
            str2.Append("vs", version);
            Windows.RunApplication("Content editor", str2.ToString());
        }

        protected void PageSize_Change()
        {
            string str = Context.ClientPage.ClientRequest.Form["PageSize"];
            int @int = MainUtil.GetInt(str, 10);
            this.PageSize = @int;
            this.Refresh();
        }

        protected void Pane_Toggle(string id)
        {
            Assert.ArgumentNotNull(id, "id");
            string str = "P" + Regex.Replace(id, @"\W", string.Empty);
            string str2 = Registry.GetString("/Current_User/Panes/" + str);
            if (Context.ClientPage.FindControl(str) == null)
            {
                IWorkflowProvider workflowProvider = Context.ContentDatabase.WorkflowProvider;
                if (workflowProvider == null)
                {
                    return;
                }
                IWorkflow workflow = workflowProvider.GetWorkflow(id);
                this.DisplayWorkflow(workflow);
            }
            if (string.IsNullOrEmpty(str2) || (str2 == "hidden"))
            {
                Registry.SetString("/Current_User/Panes/" + str, "visible");
                Context.ClientPage.ClientResponse.SetStyle(str, "display", string.Empty);
            }
            else
            {
                Registry.SetString("/Current_User/Panes/" + str, "hidden");
                Context.ClientPage.ClientResponse.SetStyle(str, "display", "none");
            }
            SheerResponse.SetReturnValue(true);
        }

        protected void Preview(string id, string language, string version)
        {
            Assert.ArgumentNotNull(id, "id");
            Assert.ArgumentNotNull(language, "language");
            Assert.ArgumentNotNull(version, "version");
            Context.ClientPage.SendMessage(this, "item:preview(id=" + id + ",language=" + language + ",version=" + version + ")");
        }

        protected virtual void Refresh()
        {
            this.Refresh(null);
        }

        protected void Refresh(Dictionary<string, string> urlArguments)
        {
            UrlString str = new UrlString(WebUtil.GetRawUrl());
            str["reload"] = "1";
            if (urlArguments != null)
            {
                foreach (KeyValuePair<string, string> pair in urlArguments)
                {
                    str[pair.Key] = pair.Value;
                }
            }
            string fullUrl = WebUtil.GetFullUrl(str.ToString());
            Context.ClientPage.ClientResponse.SetLocation(fullUrl);
        }

        private void Send(Message message)
        {
            Assert.ArgumentNotNull(message, "message");
            IWorkflowProvider workflowProvider = Context.ContentDatabase.WorkflowProvider;
            if (workflowProvider != null)
            {
                string workflowID = message["wf"];
                if (workflowProvider.GetWorkflow(workflowID) != null)
                {
                    Item item = Context.ContentDatabase.Items[message["id"], Language.Parse(message["la"]), Sitecore.Data.Version.Parse(message["vs"])];
                    if (item != null)
                    {
                        List<ItemUri> itemUris = new List<ItemUri> {
                            item.Uri
                        };
                        this.InitializeCommentDialog(itemUris, message);
                    }
                }
            }
        }

        private void SendAll(Message message)
        {
            Assert.ArgumentNotNull(message, "message");
            List<ItemUri> itemUris = new List<ItemUri>();
            string workflowID = message["wf"];
            string stateID = message["ws"];
            IWorkflowProvider workflowProvider = Context.ContentDatabase.WorkflowProvider;
            if (workflowProvider != null)
            {
                IWorkflow workflow = workflowProvider.GetWorkflow(workflowID);
                if (workflow != null)
                {
                    WorkflowState state = workflow.GetState(stateID);
                    DataUri[] items = this.GetItems(state, workflow);
                    Assert.IsNotNull(items, "uris is null");
                    if (items.Length == 0)
                    {
                        Context.ClientPage.ClientResponse.Alert("There are no selected items.");
                    }
                    else
                    {
                        itemUris = (from du in items select new ItemUri(du.ItemID, du.Language, du.Version, Context.ContentDatabase)).ToList<ItemUri>();
                        if (Settings.Workbox.WorkBoxSingleCommentForBulkOperation)
                        {
                            this.InitializeCommentDialog(itemUris, message);
                        }
                        else
                        {
                            this.ExecutCommand(itemUris, workflow, null, message["command"], message["ws"]);
                            this.Refresh();
                        }
                    }
                }
            }
        }

        private void SendSelected(Message message)
        {
            Assert.ArgumentNotNull(message, "message");
            List<ItemUri> itemUris = new List<ItemUri>();
            IWorkflowProvider workflowProvider = Context.ContentDatabase.WorkflowProvider;
            if (workflowProvider != null)
            {
                string workflowID = message["wf"];
                IWorkflow workflow = workflowProvider.GetWorkflow(workflowID);
                if (workflow != null)
                {
                    foreach (string str2 in Context.ClientPage.ClientRequest.Form.Keys)
                    {
                        if ((str2 != null) && str2.StartsWith("check_", StringComparison.InvariantCulture))
                        {
                            string str3 = "hidden_" + str2.Substring(6);
                            string[] strArray = Context.ClientPage.ClientRequest.Form[str3].Split(new char[] { ',' });
                            if (strArray.Length == 3)
                            {
                                ItemUri item = new ItemUri(strArray[0] ?? string.Empty, Language.Parse(strArray[1]), Sitecore.Data.Version.Parse(strArray[2]), Context.ContentDatabase);
                                itemUris.Add(item);
                            }
                        }
                    }
                    if (itemUris.Count == 0)
                    {
                        Context.ClientPage.ClientResponse.Alert("There are no selected items.");
                    }
                    else if (Settings.Workbox.WorkBoxSingleCommentForBulkOperation)
                    {
                        this.InitializeCommentDialog(itemUris, message);
                    }
                    else
                    {
                        this.ExecutCommand(itemUris, workflow, null, message["command"], message["ws"]);
                        this.Refresh();
                    }
                }
            }
        }

        private static void ShowHistory(Message message, string control)
        {
            Assert.ArgumentNotNull(message, "message");
            Assert.ArgumentNotNull(control, "control");
            XmlControl webControl = Resource.GetWebControl("WorkboxHistory") as XmlControl;
            Assert.IsNotNull(webControl, "history is null");
            webControl["ItemID"] = message["id"];
            webControl["Language"] = message["la"];
            webControl["Version"] = message["vs"];
            webControl["WorkflowID"] = message["wf"];
            Context.ClientPage.ClientResponse.ShowPopup(control, "below", webControl);
        }

        private void UpdateRibbon()
        {
            Ribbon child = new Ribbon
            {
                ID = "WorkboxRibbon",
                CommandContext = new CommandContext()
            };
            Item item = Context.Database.GetItem("/sitecore/content/Applications/Workbox/Ribbon");
            Error.AssertItemFound(item, "/sitecore/content/Applications/Workbox/Ribbon");
            child.CommandContext.RibbonSourceUri = item.Uri;
            child.CommandContext.CustomData = this.IsReload;
            this.RibbonPanel.Controls.Add(child);
        }

        private void WireUpNavigators(System.Web.UI.Control control)
        {
            foreach (System.Web.UI.Control control2 in control.Controls)
            {
                Navigator navigator = control2 as Navigator;
                if (navigator != null)
                {
                    navigator.Jump += new Navigator.NavigatorDelegate(this.Jump);
                    navigator.Previous += new Navigator.NavigatorDelegate(this.Jump);
                    navigator.Next += new Navigator.NavigatorDelegate(this.Jump);
                }
                this.WireUpNavigators(control2);
            }
        }

        [UsedImplicitly]
        private void WorkflowCompleteRefresh(WorkflowPipelineArgs args)
        {
            this.Refresh();
        }

        [UsedImplicitly]
        private void WorkflowCompleteStateItemCount(WorkflowPipelineArgs args)
        {
            IWorkflow workflowFromPage = this.GetWorkflowFromPage();
            if (workflowFromPage != null)
            {
                int itemCount = workflowFromPage.GetItemCount(args.PreviousState.StateID);
                if ((this.PageSize > 0) && ((itemCount % this.PageSize) == 0))
                {
                    if ((itemCount / this.PageSize) > 1)
                    {
                        this.Offset[args.PreviousState.StateID]--;
                    }
                    else
                    {
                        this.Offset[args.PreviousState.StateID] = 0;
                    }
                }
                Dictionary<string, string> urlArguments = workflowFromPage.GetStates().ToDictionary<WorkflowState, string, string>(state => state.StateID, state => this.Offset[state.StateID].ToString());
                this.Refresh(urlArguments);
            }
        }

        protected virtual bool IsReload
        {
            get
            {
                UrlString str = new UrlString(WebUtil.GetRawUrl());
                return (str["reload"] == "1");
            }
        }

        public int PageSize
        {
            get
            {
                return Registry.GetInt("/Current_User/Workbox/Page Size", 10);
            }
            set
            {
                Registry.SetInt("/Current_User/Workbox/Page Size", value);
            }
        }

        private class OffsetCollection
        {
            public int this[string key]
            {
                get
                {
                    int num2;
                    if (Context.ClientPage.ServerProperties[key] != null)
                    {
                        return (int)Context.ClientPage.ServerProperties[key];
                    }
                    UrlString str = new UrlString(WebUtil.GetRawUrl());
                    if (str[key] == null)
                    {
                        return 0;
                    }
                    if (!int.TryParse(str[key], out num2))
                    {
                        return 0;
                    }
                    return num2;
                }
                set
                {
                    Context.ClientPage.ServerProperties[key] = value;
                }
            }
        }

        protected class StateItems
        {
            public IEnumerable<string> CommandIds { get; set; }

            public IEnumerable<Item> Items { get; set; }
        }
    }
}
