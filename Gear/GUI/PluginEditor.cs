/* --------------------------------------------------------------------------------
 * Gear: Parallax Inc. Propeller Debugger
 * Copyright 2007 - Robert Vandiver
 * --------------------------------------------------------------------------------
 * PluginEditor.cs
 * Editor window for plugins class
 * --------------------------------------------------------------------------------
 *  This program is free software; you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation; either version 2 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with this program; if not, write to the Free Software
 *  Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
 * --------------------------------------------------------------------------------
 */

using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml;

using Gear.PluginSupport;

namespace Gear.GUI
{
    /// @brief Form to edit or create GEAR plugins.
    public partial class PluginEditor : Form
    {
        /// @brief File name of current plugin on editor window.
        /// @note Include full path and name to the file.
        private string _saveFileName;
        /// @brief Default font for editor code.
        /// @since v14.07.03 - Added.
        private static Font defaultFont = new Font(FontFamily.GenericMonospace, 10, 
            FontStyle.Regular);
        /// @brief Bold font for editor code.
        /// @since v15.03.26 - Added.
        private static Font fontBold = new Font(defaultFont, FontStyle.Bold);

        /// @brief Flag if the plugin definition has changed.
        /// To determine changes, it includes not only the C# code, but also class name and 
        /// reference list.
        /// @since v15.03.26 - Added.
        private bool codeChanged;
        /// @brief Enable or not change detection event.
        /// @since v15.03.26 - Added.
        private bool changeDetectEnabled;

        /// @brief Regex to looking for class name inside the code of plugin.
        /// @since v15.03.26 - Added.
        private static Regex ClassNameExpression = new Regex(
            @"\bclass\s+" +
            @"(?<classname>[@]?[_]*[A-Z|a-z|0-9]+[A-Z|a-z|0-9|_]*)" +
            @"\s*\:\s*PluginBase\b",
            RegexOptions.Compiled);
        /// @brief Regex for syntax highlight.
        /// @since v15.03.26 - Added.
        private static Regex LineExpression= new Regex(
            @"\n", 
            RegexOptions.Compiled);
        /// @brief Regex for parse token in lines for syntax highlight
        /// @version 15.03.26 - Added.
        private Regex CodeLine = new Regex(
            @"([ \t{}();:])", 
            RegexOptions.Compiled);

        /// @brief keywords to highlight in editor code
        private static readonly HashSet<string> keywords = new HashSet<string> 
        {
            "add", "abstract", "alias", "as", "ascending", "async", "await",
            "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate",
            "descending", "do", "double", "dynamic", "else", "enum", "event",
            "explicit", "extern", "false", "finally", "fixed", "float",
            "for", "foreach", "from", "get", "global", "goto", "group", "if",
            "implicit", "in", "int", "interface", "internal", "into", "is",
            "join", "let", "lock", "long", "namespace", "new", "null", "object",
            "operator", "orderby", "out", "override", "params", "partial ",
            "private", "protected", "public", "readonly", "ref", "remove",
            "return", "sbyte", "sealed", "select", "set", "short", "sizeof",
            "stackalloc", "static", "string", "struct", "switch", "this",
            "throw", "true", "try", "typeof", "uint", "ulong", "unchecked",
            "unsafe", "ushort", "using", "value", "var", "virtual", "void",
            "volatile", "where", "while", "yield"
        };
        
        /// @brief Default constructor.
        /// Initialize the class, defines columns for error grid, setting on changes detection  
        /// initially, and try to load the default template for plugin.
        /// @param[in] loadDefaultTemplate Indicate to load default template (=true) or 
        /// no template at all(=false).
        /// @since v15.03.26 - Added parameter for loading default template for plugin.
        public PluginEditor(bool loadDefaultTemplate)
        {
            InitializeComponent();

            changeDetectEnabled = false;
            if (loadDefaultTemplate)   //load default plugin template
            {
                try
                {
                    codeEditorView.LoadFile("Resources\\PluginTemplate.cs",
                        RichTextBoxStreamType.PlainText);
                }
                catch (IOException) { }         //do nothing, maintaining empty the code text box
                catch (ArgumentException) { }   //
                finally { }                     //
            }

            _saveFileName = null;
            changeDetectEnabled = true;
            CodeChanged = false;

            // setting default font
            defaultFont = new Font(FontFamily.GenericMonospace, 10, FontStyle.Regular);
            codeEditorView.Font = defaultFont;
            if (codeEditorView.Font == null)
                codeEditorView.Font = this.Font;

            //Setup error grid
            errorListView.FullRowSelect = true;
            errorListView.GridLines = true;
            errorListView.Columns.Add("Name   ", -2, HorizontalAlignment.Left);
            errorListView.Columns.Add("Line", -2, HorizontalAlignment.Right);
            errorListView.Columns.Add("Column", -2, HorizontalAlignment.Right);
            errorListView.Columns.Add("Message", -2, HorizontalAlignment.Left);

            //retrieve from settings the last state for embedded code
            SetEmbeddedCodeButton(Properties.Settings.Default.EmbeddedCode);

        }

        /// @brief Return last plugin successfully loaded o saved.
        /// @details Useful to remember last plugin directory.
        /// @since v15.03.26 - Added.
        public string GetLastPlugin
        {
            get { return _saveFileName; }
        }

        /// @brief Attribute for changed plugin detection.
        /// @since v15.03.26 - Added.
        private bool CodeChanged
        {
            get { return codeChanged; }
            set  
            {
                codeChanged = value;
                UpdateTitles();
            }
        }

        /// @brief Complete Name for plugin, including path.
        /// @since v15.03.26 - Added.
        private string SaveFileName
        {
            get
            {
                if (!String.IsNullOrEmpty(_saveFileName))
                    return new FileInfo(_saveFileName).Name;
                else return "<New plugin>";
            }
            set
            {
                _saveFileName = value;
                UpdateTitles();
            }
        }

        /// @brief Shows or hide the error grid.
        /// @param enable Enable (=true) or disable (=False) the error grid.
        public void ShowErrorGrid(bool enable)
        {
            if (enable == true)
                errorListView.Show();
            else
                errorListView.Hide();
        }

        /// @brief Update titles of window and metadata, considering modified state.
        /// @details Considering name of the plugin and showing modified state, to tell the user 
        /// if need to save.
        private void UpdateTitles()
        {
            this.Text = ("Plugin Editor: " + SaveFileName +  (CodeChanged ? " *" : string.Empty));
        }

        /// @brief Load a plugin from File.
        /// @note This method take care of update change state of the window. 
        /// @todo Correct method to implement new plugin system.
        public bool OpenFile(string FileName, bool displayErrors)
        {
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.IgnoreComments = true;
            settings.IgnoreProcessingInstructions = true;
            settings.IgnoreWhitespace = true;
            XmlReader tr = XmlReader.Create(FileName, settings);
            bool ReadText = false;

            if (referencesList.Items.Count > 0) 
                referencesList.Items.Clear();   //clear out the reference list
            try
            {

                while (tr.Read())
                {
                    if (tr.NodeType == XmlNodeType.Text && ReadText)
                    {
                        //set or reset font and color
                        codeEditorView.SelectAll();
                        codeEditorView.SelectionFont = defaultFont;
                        codeEditorView.SelectionColor = Color.Black;
                        codeEditorView.Text = tr.Value;
                        ReadText = false;
                    }

                    switch (tr.Name.ToLower())
                    {
                        case "reference":
                            if (!tr.IsEmptyElement)     //prevent empty element generates error
                                referencesList.Items.Add(tr.GetAttribute("name"));
                            break;
                        case "instance":
                            instanceName.Text = tr.GetAttribute("class");
                            break;
                        case "code":
                            ReadText = true;
                            break;
                    }
                }
                _saveFileName = FileName;
                CodeChanged = false;

                if (displayErrors)
                {
                    errorListView.Items.Clear();
                    ModuleCompiler.EnumerateErrors(EnumErrors);
                }

                return true;
            }
            catch (IOException ioe)
            {
                MessageBox.Show(this,
                    ioe.Message,
                    "Failed to load plug-in",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation);

                return false;
            }
            catch (XmlException xmle)
            {
                MessageBox.Show(this,
                    xmle.Message,
                    "Failed to load plug-in",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation);

                return false;
            }
            finally
            {
                tr.Close();
            }
        }

        /// @brief Save a XML file with the plugin information.
        /// @details Take care of update change state of the window. No need to do it in 
        /// methods who call this.
        /// @todo Correct method to implement new versioning plugin system.
        public void SaveFile(string FileName)
        {
            XmlDocument xmlDoc = new XmlDocument();

            XmlElement root = xmlDoc.CreateElement("plugin");
            xmlDoc.AppendChild(root);

            XmlElement instance = xmlDoc.CreateElement("instance");
            instance.SetAttribute("class", instanceName.Text);
            root.AppendChild(instance);

            foreach (string s in referencesList.Items)
            {
                instance = xmlDoc.CreateElement("reference");
                instance.SetAttribute("name", s);
                root.AppendChild(instance);
            }

            instance = xmlDoc.CreateElement("code");
            instance.AppendChild(xmlDoc.CreateTextNode(codeEditorView.Text));
            root.AppendChild(instance);

            xmlDoc.Save(FileName);

            _saveFileName = FileName;
            CodeChanged = false;    //update modified state for the plugin
        }

        /// @brief Method to compile C# source code to check errors on it.
        /// Actually call a C# compiler to determine errors, using references.
        /// @param[in] sender Object who called this on event.
        /// @param[in] e `EventArgs` class with a list of argument to the event call.
        private void CheckSource_Click(object sender, EventArgs e)
        {
            if (String.IsNullOrEmpty(codeEditorView.Text))
            {
                MessageBox.Show("No source code to check. Please add code.",
                    "Plugin Editor - Check source.", 
                    MessageBoxButtons.OK, 
                    MessageBoxIcon.Exclamation);
            }
            else
            {
                string aux = null;
                if (DetectClassName(codeEditorView.Text, out aux))  //class name detected?
                {
                    int i = 0;
                    instanceName.Text = aux;        //show the name found in the screen field
                    errorListView.Items.Clear();    //clear error list, if any
                    //prepare reference list
                    string[] refs = new string[referencesList.Items.Count];
                    foreach (string s in referencesList.Items)
                        refs[i++] = s;
                    try
                    {
                        PluginBase plugin = ModuleCompiler.LoadModule(codeEditorView.Text, instanceName.Text, refs, null);
                        if (plugin != null)
                        {
                            ShowErrorGrid(false);    //hide the error list
                            MessageBox.Show("Plugin compiled without errors.",
                                "Plugin Editor - Check source.",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                            plugin.Dispose();
                        }
                        else
                        {
                            ModuleCompiler.EnumerateErrors(EnumErrors);
                            ShowErrorGrid(true);    //show the error list
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Compile Error: " + ex.ToString(),
                            "Plugin Editor - Check source.",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                }
                else   //not detected class name
                {
                    instanceName.Text = "Not found!";
                    MessageBox.Show("Cannot detect main plugin class name. " +
                        "Please use \"class <YourPluginName> : PluginBase {...\" " +
                        "declaration on your source code.",
                        "Plugin Editor - Check source.",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }

        /// @brief Add error details on screen list.
        /// @param[in] e `CompileError` object.
        public void EnumErrors(CompilerError e)
        {
            ListViewItem item = new ListViewItem(e.ErrorNumber, 0);

            item.SubItems.Add(e.Line.ToString());
            item.SubItems.Add(e.Column.ToString());
            item.SubItems.Add(e.ErrorText);

            errorListView.Items.Add(item);
        }

        /// @brief Show a dialog to load a file with plugin information.
        /// @details This method checks if the previous plugin data was modified and not saved.
        /// @param[in] sender Object who called this on event.
        /// @param[in] e `EventArgs` class with a list of argument to the event call.
        private void OpenButton_Click(object sender, EventArgs e)
        {
            bool continueAnyway = true;
            if (CodeChanged)
            {
                continueAnyway = CloseAnyway(SaveFileName); //ask the user to not lost changes
            }
            if (continueAnyway)
            {
                OpenFileDialog dialog = new OpenFileDialog();
                dialog.Filter = "Gear plug-in component (*.xml)|*.xml|All Files (*.*)|*.*";
                dialog.Title = "Open Gear Plug-in...";
                if (!String.IsNullOrEmpty(_saveFileName))
                    //retrieve from last plugin edited
                    dialog.InitialDirectory = Path.GetDirectoryName(_saveFileName);
                else
                    if (!String.IsNullOrEmpty(Properties.Settings.Default.LastPlugin))
                        //retrieve from global last plugin
                        dialog.InitialDirectory = 
                            Path.GetDirectoryName(Properties.Settings.Default.LastPlugin);   

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    OpenFile(dialog.FileName, false);   //try to open the file and load to screen
                }
            }
        }

        /// @brief Show dialog to save a plugin information into file, using GEAR plugin format.
        /// @param[in] sender Object who called this on event.
        /// @param[in] e `EventArgs` class with a list of argument to the event call.
        private void SaveButton_Click(object sender, EventArgs e)
        {
            if (!String.IsNullOrEmpty(_saveFileName))
                SaveFile(_saveFileName);
            else
                SaveAsButton_Click(sender, e);

            UpdateTitles();   //update title window
        }

        /// @brief Show dialog to save a plugin information into file, using GEAR plugin format.
        /// @param[in] sender Object who called this on event.
        /// @param[in] e `EventArgs` class with a list of argument to the event call.
        private void SaveAsButton_Click(object sender, EventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "Gear plug-in component (*.xml)|*.xml|All Files (*.*)|*.*";
            dialog.Title = "Save Gear Plug-in...";
            if (!String.IsNullOrEmpty(_saveFileName))
                //retrieve from last plugin edited
                dialog.InitialDirectory = Path.GetDirectoryName(_saveFileName);
            else
                if (!String.IsNullOrEmpty(Properties.Settings.Default.LastPlugin))
                    //retrieve from global last plugin
                    dialog.InitialDirectory = 
                        Path.GetDirectoryName(Properties.Settings.Default.LastPlugin);    

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                SaveFile(dialog.FileName);
                UpdateTitles();   //update title window
            }
        }

        /// @brief Add a reference from the `ReferenceName`text box.
        /// Also update change state for the plugin module, marking as changed.
        /// @param[in] sender Object who called this on event.
        /// @param[in] e `EventArgs` class with a list of argument to the event call.
        private void addReferenceButton_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(referenceName.Text))
            {
                referencesList.Items.Add(referenceName.Text);
                referenceName.Text = string.Empty;
                CodeChanged = true;
            }
        }

        /// @brief Remove the selected reference of the list.
        /// Also update change state for the plugin module, marking as changed.
        /// @param[in] sender Object who called this on event.
        /// @param[in] e `EventArgs` class with a list of argument to the event call.
        private void RemoveReferenceButton_Click(object sender, EventArgs e)
        {
            if (referencesList.SelectedIndex != -1)
            {
                referencesList.Items.RemoveAt(referencesList.SelectedIndex);
                CodeChanged = true;
            }
        }

        /// @brief Position the cursor in code window, corresponding to selected error row.
        /// @param[in] sender Object who called this on event.
        /// @param[in] e EventArgs class with a list of argument to the event call.
        private void ErrorView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (errorListView.SelectedIndices.Count < 1)
                return;

            int i = 0;
            int index = errorListView.SelectedIndices[0];   //determine the current row
            ListViewItem lvi = errorListView.Items[index];
            try
            {
                int line = Convert.ToInt32(lvi.SubItems[1].Text) - 1;
                if (line < 0)
                    line = 0;
                int column = Convert.ToInt32(lvi.SubItems[2].Text) - 1;
                if (column < 0)
                    column = 0;
                while (line != codeEditorView.GetLineFromCharIndex(i++)) ;
                i += column;
                codeEditorView.SelectionStart = i;
                codeEditorView.ScrollToCaret();
                codeEditorView.Select();
            }
            catch (FormatException) { } //on errors do nothing
            return;
        }

        /// @brief Check syntax on the C# source code
        /// @param[in] sender Object who called this on event.
        /// @param[in] e `EventArgs` class with a list of argument to the event call.
        /// @since V14.07.03 - Added.
        /// @note Experimental highlighting. Probably changes in the future.
        // Syntax highlighting
        private void syntaxButton_Click(object sender, EventArgs e)
        {
            int restore_pos = codeEditorView.SelectionStart, pos = 0;    //remember last position
            changeDetectEnabled = false;    //not enable change detection
            bool commentMultiline = false;  //initially not in comment mode
            //For each line in input, identify key words and format them when 
            // adding to the rich text box.
            String[] lines = LineExpression.Split(codeEditorView.Text);
            //update progress bar
            progressHighlight.Maximum = lines.Length;
            progressHighlight.Value = 0;
            progressHighlight.Visible = true;
            //update editor code
            codeEditorView.Visible = false;
            codeEditorView.SelectAll();
            codeEditorView.Enabled = false;
            foreach (string line in lines)
            {
                progressHighlight.Value = ++pos;
                ParseLine(line, ref commentMultiline);   //remember comment mode between lines
            }
            //update progress bar
            progressHighlight.Visible = false;
            //update editor code
            codeEditorView.SelectionStart = restore_pos;    //restore last position
            codeEditorView.ScrollToCaret();                 //and scroll to it
            codeEditorView.Visible = true;
            codeEditorView.Enabled = true;
            changeDetectEnabled = true; //restore change detection
        }

        /// @brief Auxiliary method to check syntax.
        /// Examines line by line, parsing reserved C# words.
        /// @param[in] line Text line from the source code.
        /// @param[in,out] commentMultiline Flag to indicate if it is on comment mode 
        /// between multi lines (=true) or normal mode (=false).
        /// @since v14.07.03 - Added.
        /// @warning Experimental highlighting. Probably will be changes in the future.
        private void ParseLine(string line, ref bool commentMultiline)
        {
            int index;

            if (commentMultiline)
            {
                // Check for a c style end comment
                index = line.IndexOf("*/");
                if (index != -1)    //found end comment in this line?
                {
                    string comment = line.Substring(0, (index += 2));
                    codeEditorView.SelectionColor = Color.Green;
                    codeEditorView.SelectionFont = defaultFont;
                    codeEditorView.SelectedText = comment;
                    //parse the rest of the line (if any)
                    commentMultiline = false;
                    //is there more text after the comment?
                    if (line.Length > index)    
                        ParseLine(line.Substring(index), ref commentMultiline);
                    else  //no more text, so ...
                        codeEditorView.SelectedText = "\n"; //..finalize the line
                }
                else  //not end comment in this line..
                {
                    //.. so all the line will be a comment
                    codeEditorView.SelectionColor = Color.Green;
                    codeEditorView.SelectionFont = defaultFont;
                    codeEditorView.SelectedText = line;
                    codeEditorView.SelectedText = "\n"; //finalize the line
                }
            }
            else  //we are not in comment multi line mode
            {
                bool putEndLine = true;
                string[] tokens = CodeLine.Split(line);
                //parse the line searching tokens
                foreach (string token in tokens)
                {
                    // Check for a c style comment opening
                    if (token == "/*" || token.StartsWith("/*"))
                    {
                        index = line.IndexOf("/*");
                        int indexEnd = line.IndexOf("*/");
                        //end comment found in the rest of the line?
                        if ((indexEnd != -1) && (indexEnd > index)) 
                        {
                            //extract the comment text
                            string comment = line.Substring(index, (indexEnd += 2) - index);
                            codeEditorView.SelectionColor = Color.Green;
                            codeEditorView.SelectionFont = defaultFont;
                            codeEditorView.SelectedText = comment;
                            //is there more text after the comment?
                            if (line.Length > indexEnd)
                            {
                                ParseLine(line.Substring(indexEnd), ref commentMultiline);
                                putEndLine = false;
                            }
                            break;
                        }
                        else  //as there is no end comment in the line..
                        {
                            //..entering comment multi line mode
                            commentMultiline = true; 
                            string comment = line.Substring(index, line.Length - index);
                            codeEditorView.SelectionColor = Color.Green;
                            codeEditorView.SelectionFont = defaultFont;
                            codeEditorView.SelectedText = comment;
                            break;  
                        }
                    }

                    // Check for a c++ style comment.
                    if (token == "//" || token.StartsWith("//"))
                    {
                        // Find the start of the comment and then extract the whole comment.
                        index = line.IndexOf("//");
                        string comment = line.Substring(index, line.Length - index);
                        codeEditorView.SelectionColor = Color.Green;
                        codeEditorView.SelectionFont = defaultFont;
                        codeEditorView.SelectedText = comment;
                        break;
                    }

                    // Set the token's default color and font.
                    codeEditorView.SelectionColor = Color.Black;
                    codeEditorView.SelectionFont = defaultFont;
                    // Check whether the token is a keyword.
                    if (keywords.Contains(token))
                    {
                        // Apply alternative color and font to highlight keyword.
                        codeEditorView.SelectionColor = Color.Blue;
                        codeEditorView.SelectionFont = fontBold;
                    }
                    codeEditorView.SelectedText = token;
                }
                if (putEndLine)
                    codeEditorView.SelectedText = "\n";
            }
        }

        /// @brief Update change state for code text box.
        /// It marks as changed, to prevent not averted loses at closure of the window.
        /// @param[in] sender Object who called this on event.
        /// @param[in] e `EventArgs` class with a list of argument to the event call.
        /// @since v15.03.26 - Added.
        private void codeEditorView_TextChanged(object sender, EventArgs e)
        {
            if (changeDetectEnabled)
            {
                CodeChanged = true;
            }
        }

        /// @brief Detect the plugin class name from the code text given as parameter.
        /// @param[in] code Text of the source code of plugin to look for the class 
        /// name declaration.
        /// @param[out] match Name of the plugin class found. If not, it will be null.
        /// @returns If a match had found =True, else =False.
        /// @since v15.03.26 - Added.
        private bool DetectClassName(string code, out string match)
        {
            string aux = null;
            match = null;
            //Look for a 'suspect' for class definition to show it to user later.
            Match suspect = ClassNameExpression.Match(code);
            if (suspect.Success)  //if a match is found
            {
                //detect class name from the detected groups
                aux = suspect.Groups["classname"].Value;  
                if (String.IsNullOrEmpty(aux))
                {
                    return false;
                }
                else
                {
                    match = aux;
                    return true;
                }
            }
            else
                return false;
        }

        /// @brief Event handler for closing plugin window.
        /// If code, references or class name have changed and them are not saved, a Dialog is 
        /// presented to the user to proceed or abort the closing.
        /// @param[in] sender Object who called this on event.
        /// @param[in] e `FormClosingEventArgs` class with a list of argument to the event call.
        /// @since v15.03.26 - Added.
        private void PluginEditor_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (CodeChanged)
            {
                if (!CloseAnyway(SaveFileName)) //ask the user to not loose changes
                    e.Cancel = true;    //cancel the closing event
            }
            Properties.Settings.Default.LastPlugin = GetLastPlugin;
            Properties.Settings.Default.Save();
        }

        /// @brief Ask the user to not loose changes.
        /// @param[in] fileName Filename to show in dialog.
        /// @returns Boolean to close (=true) or not (=false).
        /// @since v15.03.26 - Added.
        private bool CloseAnyway(string fileName)
        {
            //dialog to not lost changes
            DialogResult confirm = MessageBox.Show(
                "Are you sure to close plugin \"" + fileName + 
                "\" without saving?\nYour changes will lost.",
                "Save.",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Exclamation,
                MessageBoxDefaultButton.Button2
            );
            if (confirm == DialogResult.OK)
                return true;
            else
                return false;
        }

        /// @brief Toggle the button state, updating the name & tooltip text.
        /// @param[in] sender Object who called this on event.
        /// @param[in] e `EventArgs` class with a list of argument to the event call.
        /// @since v15.03.26 - Added.
        private void embeddedCode_Click(object sender, EventArgs e)
        {
            SetEmbeddedCodeButton(EmbeddedCode.Checked);
            //remember setting
            Properties.Settings.Default.EmbeddedCode = EmbeddedCode.Checked;
        }

        /// @brief Update the name & tooltip text depending on each state.
        /// @param[in] newValue Value to set.
        /// @since v15.03.26 - Added.
        private void SetEmbeddedCodeButton(bool newValue)
        {
            EmbeddedCode.Checked = newValue;
            if (EmbeddedCode.Checked)
            {
                EmbeddedCode.Text = "Embedded";
                EmbeddedCode.ToolTipText = "Embedded code in XML plugin file.";
            }
            else
            {
                EmbeddedCode.Text = "Separated";
                EmbeddedCode.ToolTipText = "Code in separated file from XML plugin file.";
            }
        }

    }
}