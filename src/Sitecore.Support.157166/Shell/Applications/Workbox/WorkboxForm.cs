namespace Sitecore.Support.Shell.Applications.Workbox
{
    using Sitecore;
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
    using System.Collections.Specialized;
    using System.Linq;
    using System.Text.RegularExpressions;

    public class WorkboxForm : BaseForm
    {
        private OffsetCollection Offset = new OffsetCollection();
        protected Border Pager;
        protected Border RibbonPanel;
        private NameValueCollection stateNames;
        protected Border States;
        protected Toolmenubutton ViewMenu;

        public void Comment(ClientPipelineArgs args)
        {
            Assert.ArgumentNotNull(args, "args");
            ID @null = ID.Null;
            if (Context.ClientPage.ServerProperties["command"] != null)
            {
                ID.TryParse((string)(Context.ClientPage.ServerProperties["command"] as string), out @null);
            }
            ItemUri itemUri = new ItemUri((Context.ClientPage.ServerProperties["id"] ?? string.Empty).ToString(), Language.Parse((string)(Context.ClientPage.ServerProperties["language"] as string)), Sitecore.Data.Version.Parse((string)(Context.ClientPage.ServerProperties["version"] as string)), Context.ContentDatabase);
            bool flag = ((args.Parameters["ui"] != null) && (args.Parameters["ui"] == "1")) ? ((bool)true) : ((args.Parameters["suppresscomment"] == null) ? ((bool)false) : ((bool)(args.Parameters["suppresscomment"] == "1")));
            if ((!args.IsPostBack && (@null.IsNull)) && !flag)
            {
                WorkflowUIHelper.DisplayCommentDialog(itemUri, @null);
                args.WaitForPostBack();
            }
            else if ((args.Result != null) && (args.Result.Length > 0x7d0))
            {
                Context.ClientPage.ClientResponse.ShowError(new System.Exception(string.Format("The comment is too long.\n\nYou have entered {0} characters.\nA comment cannot contain more than 2000 characters.", (int)args.Result.Length)));
                WorkflowUIHelper.DisplayCommentDialog(itemUri, @null);
                args.WaitForPostBack();
            }
            else if ((((args.Result != null) && (args.Result != "null")) && ((args.Result != "undefined") && (args.Result != "cancel"))) || flag)
            {
                string result = args.Result;
                Sitecore.Collections.StringDictionary commentFields = null;
                if (!string.IsNullOrEmpty(result))
                {
                    commentFields = WorkflowUIHelper.ExtractFieldsFromFieldEditor(result);
                }
                else
                {
                    commentFields = new Sitecore.Collections.StringDictionary();
                }
                try
                {
                    IWorkflow workflowFromPage = this.GetWorkflowFromPage();
                    if (workflowFromPage != null)
                    {
                        Item item = Database.GetItem(itemUri);
                        if (item != null)
                        {
                            Processor completionCallback = new Processor("Workflow complete state item count", this, "WorkflowCompleteStateItemCount");
                            WorkflowUIHelper.ExecuteCommand(item, workflowFromPage, (string)(Context.ClientPage.ServerProperties["command"] as string), commentFields, completionCallback);
                        }
                    }
                }
                catch (WorkflowStateMissingException)
                {
                    SheerResponse.Alert("One or more items could not be processed because their workflow state does not specify the next step.", new string[0]);
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
            System.Text.StringBuilder builder = new System.Text.StringBuilder(" - (");
            Language language = item.Language;
            builder.Append(language.CultureInfo.DisplayName);
            builder.Append(", ");
            builder.Append(Translate.Text("version"));
            builder.Append(' ');
            builder.Append(item.Version.ToString());
            builder.Append(")");
            Assert.IsNotNull(webControl, "workboxItem");
            WorkflowEvent[] history = workflow.GetHistory(item);
            webControl["Header"] = item.DisplayName;
            webControl["Details"] = builder.ToString();
            webControl["Icon"] = item.Appearance.Icon;
            webControl["ShortDescription"] = Settings.ContentEditor.RenderItemHelpAsHtml ? WebUtil.RemoveAllScripts(item.Help.ToolTip) : System.Web.HttpUtility.HtmlEncode(item.Help.ToolTip);
            webControl["History"] = this.GetHistory(workflow, history);
            webControl["LastComments"] = System.Web.HttpUtility.HtmlEncode(this.GetLastComments(history, item));
            webControl["HistoryMoreID"] = Sitecore.Web.UI.HtmlControls.Control.GetUniqueID("ctl");
            webControl["HistoryClick"] = string.Concat((object[])new object[] { "workflow:showhistory(id=", item.ID, ",la=", item.Language.Name, ",vs=", item.Version, ",wf=", workflow.WorkflowID, ")" });
            webControl["PreviewClick"] = string.Concat((object[])new object[] { "Preview(\"", item.ID, "\", \"", item.Language, "\", \"", item.Version, "\")" });
            webControl["Click"] = string.Concat((object[])new object[] { "Open(\"", item.ID, "\", \"", item.Language, "\", \"", item.Version, "\")" });
            webControl["DiffClick"] = string.Concat((object[])new object[] { "Diff(\"", item.ID, "\", \"", item.Language, "\", \"", item.Version, "\")" });
            webControl["Display"] = "none";
            string uniqueID = Sitecore.Web.UI.HtmlControls.Control.GetUniqueID(string.Empty);
            webControl["CheckID"] = "check_" + uniqueID;
            webControl["HiddenID"] = "hidden_" + uniqueID;
            webControl["CheckValue"] = string.Concat((object[])new object[] { item.ID, ",", item.Language, ",", item.Version });
            WorkflowCommand[] commandArray = WorkflowFilterer.FilterVisibleCommands(workflow.GetCommands(item), item);
            for (int i = 0; i < commandArray.Length; i = (int)(i + 1))
            {
                WorkflowCommand command = commandArray[i];
                this.CreateCommand(workflow, command, item, webControl);
            }
        }

        private void CreateNavigator(Sitecore.Web.UI.HtmlControls.Section section, string id, int count, int offset)
        {
            Assert.ArgumentNotNull(section, "section");
            Assert.ArgumentNotNull(id, "id");
            Navigator navigator = new Navigator();
            section.Controls.Add(navigator);
            navigator.ID = id;
            navigator.Offset = offset;
            navigator.Count = count;
            navigator.PageSize = this.PageSize;
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

        protected virtual void DisplayState(IWorkflow workflow, WorkflowState state, StateItems stateItems, System.Web.UI.Control control, int offset, int pageSize)
        {
            Assert.ArgumentNotNull(workflow, "workflow");
            Assert.ArgumentNotNull(state, "state");
            Assert.ArgumentNotNull(stateItems, "stateItems");
            Assert.ArgumentNotNull(control, "control");
            Item[] itemArray = stateItems.Items.ToArray<Item>();
            if (itemArray.Length > 0)
            {
                int length = (int)(offset + pageSize);
                if (length > itemArray.Length)
                {
                    length = (int)itemArray.Length;
                }
                for (int i = offset; i < length; i = (int)(i + 1))
                {
                    this.CreateItem(workflow, itemArray[i], control);
                }
                Border border = new Border
                {
                    Background = "#fff"
                };
                control.Controls.Add(border);
                border.Margin = "0 5px 10px 15px";
                border.Padding = "5px 10px";
                border.Class = "scWorkboxToolbarButtons";
                WorkflowCommand[] commands = workflow.GetCommands(state.StateID);
                for (int j = 0; j < commands.Length; j = (int)(j + 1))
                {
                    WorkflowCommand command = commands[j];
                    if (Enumerable.Contains<string>(stateItems.CommandIds, command.CommandID))
                    {
                        XmlControl webControl = Resource.GetWebControl("WorkboxCommand") as XmlControl;
                        Assert.IsNotNull(webControl, "workboxCommand is null");
                        webControl["Header"] = command.DisplayName + " " + Translate.Text("(selected)");
                        webControl["Icon"] = command.Icon;
                        webControl["Command"] = string.Concat((string[])new string[] { "workflow:sendselected(command=", command.CommandID, ",ws=", state.StateID, ",wf=", workflow.WorkflowID, ")" });
                        border.Controls.Add(webControl);
                        webControl = Resource.GetWebControl("WorkboxCommand") as XmlControl;
                        Assert.IsNotNull(webControl, "workboxCommand is null");
                        webControl["Header"] = command.DisplayName + " " + Translate.Text("(all)");
                        webControl["Icon"] = command.Icon;
                        webControl["Command"] = string.Concat((string[])new string[] { "workflow:sendall(command=", command.CommandID, ",ws=", state.StateID, ",wf=", workflow.WorkflowID, ")" });
                        border.Controls.Add(webControl);
                    }
                }
            }
        }

        protected virtual void DisplayStates(IWorkflow workflow, XmlControl placeholder)
        {
            Assert.ArgumentNotNull(workflow, "workflow");
            Assert.ArgumentNotNull(placeholder, "placeholder");
            this.stateNames = null;
            WorkflowState[] states = workflow.GetStates();
            for (int i = 0; i < states.Length; i = (int)(i + 1))
            {
                WorkflowState state = states[i];
                StateItems stateItems = this.GetStateItems(state, workflow);
                Assert.IsNotNull(stateItems, "stateItems is null");
                if (Enumerable.Any<string>(stateItems.CommandIds))
                {
                    string str2;
                    string str = ShortID.Encode(workflow.WorkflowID) + "_" + ShortID.Encode(state.StateID);
                    Sitecore.Web.UI.HtmlControls.Section section2 = new Sitecore.Web.UI.HtmlControls.Section();
                    section2.ID = str + "_section";
                    Sitecore.Web.UI.HtmlControls.Section control = section2;
                    placeholder.AddControl(control);
                    int count = Enumerable.Count<Item>(stateItems.Items);
                    if (count <= 0)
                    {
                        str2 = Translate.Text("None");
                    }
                    else if (count == 1)
                    {
                        str2 = string.Format("1 {0}", Translate.Text("item"));
                    }
                    else
                    {
                        str2 = string.Format("{0} {1}", (int)count, Translate.Text("items"));
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
                    control.Collapsed = (bool)(count <= 0);
                    Border border = new Border();
                    control.Controls.Add(border);
                    border.ID = str + "_content";
                    this.DisplayState(workflow, state, stateItems, border, this.Offset[state.StateID], this.PageSize);
                    this.CreateNavigator(control, str + "_navigator", count, this.Offset[state.StateID]);
                }
            }
        }

        protected virtual void DisplayWorkflow(IWorkflow workflow)
        {
            Assert.ArgumentNotNull(workflow, "workflow");
            Context.ClientPage.ServerProperties["WorkflowID"] = workflow.WorkflowID;
            XmlControl webControl = Resource.GetWebControl("Pane") as XmlControl;
            Sitecore.Diagnostics.Error.AssertXmlControl(webControl, "Pane");
            this.States.Controls.Add(webControl);
            Assert.IsNotNull(webControl, "pane");
            webControl["PaneID"] = this.GetPaneID(workflow);
            webControl["Header"] = workflow.Appearance.DisplayName;
            webControl["Icon"] = workflow.Appearance.Icon;
            FeedUrlOptions options = new FeedUrlOptions("/sitecore/shell/~/feed/workflow.aspx")
            {
                UseUrlAuthentication = true
            };
            webControl["TextID"] = webControl.ID;
            options.Parameters["wf"] = workflow.WorkflowID;
            webControl["FeedLink"] = options.ToString();
            this.DisplayStates(workflow, webControl);
            if (Context.ClientPage.IsEvent)
            {
                SheerResponse.Insert(this.States.ClientID, "append", HtmlUtil.RenderControl(webControl));
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
                if (user.StartsWith(name + @"\", System.StringComparison.OrdinalIgnoreCase))
                {
                    user = StringUtil.Mid(user, (int)(name.Length + 1));
                }
                user = StringUtil.GetString((string[])new string[] { user, Translate.Text("Unknown") });
                string stateName = this.GetStateName(workflow, event2.OldState);
                string str5 = this.GetStateName(workflow, event2.NewState);
                return string.Format(Translate.Text("{0} changed from <b>{1}</b> to <b>{2}</b> on {3}."), new object[] { user, stateName, str5, DateUtil.FormatDateTime(DateUtil.ToServerTime(event2.Date), "D", Context.User.Profile.Culture) });
            }
            return Translate.Text("No changes have been made.");
        }

        private DataUri[] GetItems(WorkflowState state, IWorkflow workflow)
        {
            Assert.ArgumentNotNull(state, "state");
            Assert.ArgumentNotNull(workflow, "workflow");
            System.Collections.ArrayList list = new System.Collections.ArrayList();
            DataUri[] items = workflow.GetItems(state.StateID);
            if (items != null)
            {
                DataUri[] uriArray2 = items;
                for (int i = 0; i < uriArray2.Length; i = (int)(i + 1))
                {
                    DataUri uri = uriArray2[i];
                    Item item = Context.ContentDatabase.Items[uri];
                    if ((((item != null) && item.Access.CanRead()) && (item.Access.CanReadLanguage() && item.Access.CanWriteLanguage())) && ((Context.IsAdministrator || item.Locking.CanLock()) || item.Locking.HasLock()))
                    {
                        list.Add(uri);
                    }
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
            System.Collections.Generic.List<Item> list = new System.Collections.Generic.List<Item>();
            System.Collections.Generic.List<string> list2 = new System.Collections.Generic.List<string>();
            DataUri[] items = workflow.GetItems(state.StateID);
            bool flag = (bool)(items.Length > Settings.Workbox.StateCommandFilteringItemThreshold);
            if (items != null)
            {
                DataUri[] uriArray2 = items;
                for (int i = 0; i < uriArray2.Length; i = (int)(i + 1))
                {
                    DataUri uri = uriArray2[i];
                    Item item = Context.ContentDatabase.GetItem(uri);
                    if ((((item != null) && item.Access.CanRead()) && (item.Access.CanReadLanguage() && item.Access.CanWriteLanguage())) && ((Context.IsAdministrator || item.Locking.CanLock()) || item.Locking.HasLock()))
                    {
                        list.Add(item);
                        if (!flag)
                        {
                            WorkflowCommand[] commandArray3 = WorkflowFilterer.FilterVisibleCommands(workflow.GetCommands(item), item);
                            for (int j = 0; j < commandArray3.Length; j = (int)(j + 1))
                            {
                                WorkflowCommand command = commandArray3[j];
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
                WorkflowState[] states = workflow.GetStates();
                for (int i = 0; i < states.Length; i = (int)(i + 1))
                {
                    WorkflowState state = states[i];
                    this.stateNames.Add(state.StateID, state.DisplayName);
                }
            }
            return StringUtil.GetString((string[])new string[] { this.stateNames[stateID], "?" });
        }

        private IWorkflow GetWorkflowFromPage()
        {
            IWorkflowProvider workflowProvider = Context.ContentDatabase.WorkflowProvider;
            if (workflowProvider == null)
            {
                return null;
            }
            return workflowProvider.GetWorkflow((string)(Context.ClientPage.ServerProperties["workflowid"] as string));
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
                string name = StringUtil.GetString((string[])new string[] { message["language"] });
                string str3 = StringUtil.GetString((string[])new string[] { message["version"] });
                Item item = Context.ContentDatabase.Items[str, Language.Parse(name), Sitecore.Data.Version.Parse(str3)];
                if (item != null)
                {
                    Dispatcher.Dispatch(message, item);
                }
            }
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
            Sitecore.Diagnostics.Error.Assert((bool)(workflow != null), "Workflow \"" + workflowID + "\" not found.");
            Assert.IsNotNull(workflow, "workflow");
            WorkflowState state = workflow.GetState(stateID);
            Assert.IsNotNull(state, "Workflow state \"" + stateID + "\" not found.");
            Border border2 = new Border();
            border2.ID = control + "_content";
            Border border = border2;
            StateItems stateItems = this.GetStateItems(state, workflow);
            this.DisplayState(workflow, state, stateItems ?? new StateItems(), border, offset, this.PageSize);
            Context.ClientPage.ClientResponse.SetOuterHtml(control + "_content", border);
        }

        protected override void OnLoad(System.EventArgs e)
        {
            Assert.ArgumentNotNull(e, "e");
            base.OnLoad(e);
            if (!Context.ClientPage.IsEvent)
            {
                IWorkflowProvider workflowProvider = Context.ContentDatabase.WorkflowProvider;
                if (workflowProvider != null)
                {
                    IWorkflow[] workflows = workflowProvider.GetWorkflows();
                    IWorkflow[] workflowArray2 = workflows;
                    for (int i = 0; i < workflowArray2.Length; i = (int)(i + 1))
                    {
                        IWorkflow workflow = workflowArray2[i];
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
                IWorkflow[] workflows = workflowProvider.GetWorkflows();
                for (int i = 0; i < workflows.Length; i = (int)(i + 1))
                {
                    IWorkflow workflow = workflows[i];
                    string paneID = this.GetPaneID(workflow);
                    string str2 = Registry.GetString("/Current_User/Panes/" + paneID);
                    control.Add(Sitecore.Web.UI.HtmlControls.Control.GetUniqueID("ctl"), workflow.Appearance.DisplayName, workflow.Appearance.Icon, string.Empty, ((str2 != "hidden") ? ((string)"workbox:hide") : ((string)"workbox:show")) + "(id=" + paneID + ")", (bool)(str2 != "hidden"), string.Empty, MenuItemType.Check);
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
            int @int = MainUtil.GetInt(Context.ClientPage.ClientRequest.Form["PageSize"], 10);
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
            Context.ClientPage.SendMessage(this, string.Concat((string[])new string[] { "item:preview(id=", id, ",language=", language, ",version=", version, ")" }));
        }

        protected void Refresh()
        {
            this.Refresh(null);
        }

        protected void Refresh(System.Collections.Generic.Dictionary<string, string> urlArguments)
        {
            UrlString str = new UrlString(WebUtil.GetRawUrl());
            str["reload"] = "1";
            if (urlArguments != null)
            {
                foreach (System.Collections.Generic.KeyValuePair<string, string> pair in urlArguments)
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
                if ((workflowProvider.GetWorkflow(workflowID) != null) && (Context.ContentDatabase.Items[message["id"], Language.Parse(message["la"]), Sitecore.Data.Version.Parse(message["vs"])] != null))
                {
                    Context.ClientPage.ServerProperties["id"] = message["id"];
                    Context.ClientPage.ServerProperties["language"] = message["la"];
                    Context.ClientPage.ServerProperties["version"] = message["vs"];
                    Context.ClientPage.ServerProperties["command"] = message["command"];
                    Context.ClientPage.ServerProperties["workflowid"] = workflowID;
                    NameValueCollection parameters = new NameValueCollection();
                    parameters.Add("ui", message["ui"]);
                    parameters.Add("suppresscomment", message["suppresscomment"]);
                    Context.ClientPage.Start(this, "Comment", parameters);
                }
            }
        }

        private void SendAll(Message message)
        {
            Assert.ArgumentNotNull(message, "message");
            IWorkflowProvider workflowProvider = Context.ContentDatabase.WorkflowProvider;
            if (workflowProvider != null)
            {
                string workflowID = message["wf"];
                string stateID = message["ws"];
                IWorkflow workflow = workflowProvider.GetWorkflow(workflowID);
                if (workflow != null)
                {
                    WorkflowState state = workflow.GetState(stateID);
                    DataUri[] items = this.GetItems(state, workflow);
                    Assert.IsNotNull(items, "uris is null");
                    if (state == null)
                    {
                    }
                    else
                    {
                        string displayName = state.DisplayName;
                    }
                    bool flag = false;
                    DataUri[] uriArray2 = items;
                    for (int i = 0; i < uriArray2.Length; i = (int)(i + 1))
                    {
                        DataUri uri = uriArray2[i];
                        Item item = Context.ContentDatabase.Items[uri];
                        if (item != null)
                        {
                            try
                            {
                                Processor completionCallback = new Processor("Workflow complete refresh", this, "WorkflowCompleteRefresh");
                                WorkflowUIHelper.ExecuteCommand(item, workflow, message["command"], null, completionCallback);
                            }
                            catch (WorkflowStateMissingException)
                            {
                                flag = true;
                            }
                        }
                    }
                    if (flag)
                    {
                        SheerResponse.Alert("One or more items could not be processed because their workflow state does not specify the next step.", new string[0]);
                    }
                }
            }
        }

        private void SendSelected(Message message)
        {
            Assert.ArgumentNotNull(message, "message");
            IWorkflowProvider workflowProvider = Context.ContentDatabase.WorkflowProvider;
            if (workflowProvider != null)
            {
                string workflowID = message["wf"];
                string str2 = message["ws"];
                IWorkflow workflow = workflowProvider.GetWorkflow(workflowID);
                if (workflow != null)
                {
                    int num = 0;
                    bool flag = false;
                    foreach (string str3 in Context.ClientPage.ClientRequest.Form.Keys)
                    {
                        if ((str3 != null) && str3.StartsWith("check_", System.StringComparison.InvariantCulture))
                        {
                            string str4 = "hidden_" + str3.Substring(6);
                            string[] strArray = Context.ClientPage.ClientRequest.Form[str4].Split((char[])new char[] { ',' });
                            Item item = Context.ContentDatabase.Items[strArray[0], Language.Parse(strArray[1]), Sitecore.Data.Version.Parse(strArray[2])];
                            if (item != null)
                            {
                                WorkflowState state = workflow.GetState(item);
                                if (state.StateID == str2)
                                {
                                    try
                                    {
                                        workflow.Execute(message["command"], item, state.DisplayName, true, new object[0]);
                                    }
                                    catch (WorkflowStateMissingException)
                                    {
                                        flag = true;
                                    }
                                    num = (int)(num + 1);
                                }
                            }
                        }
                    }
                    if (flag)
                    {
                        SheerResponse.Alert("One or more items could not be processed because their workflow state does not specify the next step.", new string[0]);
                    }
                    if (num == 0)
                    {
                        Context.ClientPage.ClientResponse.Alert("There are no selected items.");
                    }
                    else
                    {
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
            Ribbon ribbon2 = new Ribbon();
            ribbon2.ID = "WorkboxRibbon";
            ribbon2.CommandContext = new CommandContext();
            Ribbon ribbon = ribbon2;
            Item item = Context.Database.GetItem("/sitecore/content/Applications/Workbox/Ribbon");
            Sitecore.Diagnostics.Error.AssertItemFound(item, "/sitecore/content/Applications/Workbox/Ribbon");
            ribbon.CommandContext.RibbonSourceUri = item.Uri;
            ribbon.CommandContext.CustomData = (bool)this.IsReload;
            this.RibbonPanel.Controls.Add(ribbon);
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
                        this.Offset[args.PreviousState.StateID] = (int)(this.Offset[args.PreviousState.StateID] - 1);
                    }
                    else
                    {
                        this.Offset[args.PreviousState.StateID] = 0;
                    }
                }
                System.Collections.Generic.Dictionary<string, string> urlArguments = workflowFromPage.GetStates().ToDictionary<WorkflowState, string, string>(state => state.StateID, state => ((int)this.Offset[state.StateID]).ToString());
                this.Refresh(urlArguments);
            }
        }

        protected virtual bool IsReload
        {
            get
            {
                UrlString str = new UrlString(WebUtil.GetRawUrl());
                return (bool)(str["reload"] == "1");
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
                        return (int)((int)Context.ClientPage.ServerProperties[key]);
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
                    Context.ClientPage.ServerProperties[key] = (int)value;
                }
            }
        }

        protected class StateItems
        {
            public System.Collections.Generic.IEnumerable<string> CommandIds { get; set; }

            public System.Collections.Generic.IEnumerable<Item> Items { get; set; }
        }
    }
}
