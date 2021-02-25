using OpenQA.Selenium;
using System;
using System.Windows.Forms;
using System.Configuration;
using System.IO;
using System.Collections.Generic;
using Microsoft.Win32;
using System.Diagnostics;
using OpenQA.Selenium.Chrome;
using System.Linq;
using System.Threading;
using Timer = System.Timers.Timer;
using OpenQA.Selenium.Support.UI;
using System.Timers;

namespace ParlerFollow
{
    public partial class Form1 : Form
    {

        IWebDriver driver;
        Configuration config;
        Timer displayTimer;
        Stopwatch _timer = new Stopwatch();
        string chromeVersion;
        string appPath = Directory.GetCurrentDirectory();
        string keyword;

        int totalvotes = 0, NumberofRun;

        Thread follower, upvoter;
        ManualResetEvent _stopEvent = new ManualResetEvent(false);
        ManualResetEvent _pauseEvent = new ManualResetEvent(false);

        bool isRunning = false, restricted = false, KeyRegistered = false;
        public delegate void OutputDelegate(string element, string value);

        public Form1()
        {
            InitializeComponent();
            config = ConfigurationManager.OpenExeConfiguration(Application.ExecutablePath);

        }
        private void Form1_Load(object sender, EventArgs e)
        {

            RegistryKey rkey = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\PalerSettings");

            if (rkey!= null)
            {
                var keyValue = rkey.GetValue("NumberofRun");
                if (keyValue !=null)
                {
                    NumberofRun = (int)keyValue;
                } else
                {
                    NumberofRun = 0;
                    rkey.SetValue("NumberofRun", 0);
                }

                keyValue = rkey.GetValue("KeyRegistered_voter");
                if (keyValue != null)
                {
                    KeyRegistered = Convert.ToBoolean(keyValue);
                }
            }

            if (NumberofRun > 100)
            {
                restricted = true;
            }
            NumberofRun++;
            totalvotes = 0;
            foreach (Browser browser in GetBrowsers())
            {
                if (browser.Name.Contains("Chrome"))
                {
                    chromeVersion = browser.Version;
                    break;
                }
            }
            btn_Start.Enabled = false;
            btn_Pause.Enabled = false;
            btn_Stop.Enabled = false;


            try
            {
                txt_Keyword.Text = ReadSetting("follow_Keyword");
                if (txt_Keyword.Text == "" || txt_Keyword.Text == "0") txt_Keyword.Text = "Trump";
            }
            catch (Exception ex)
            {
                txt_Keyword.Text = "Trump";
            }


        }
        private void btn_Login_Click(object sender, EventArgs e)
        {
            ChromeDriverService driverService = null;
            driverService = ChromeDriverService.CreateDefaultService(appPath);

            if (string.IsNullOrEmpty(txt_Useremail.Text) || string.IsNullOrEmpty(txt_Password.Text))
            {
                MessageBox.Show("Please Input the Username and password.");
                return;
            }
            driverService.HideCommandPromptWindow = true;
            ChromeOptions chromeOptions = new ChromeOptions();

            //chromeOptions.AddArgument("user-data-dir=C:\\Users\\micha\\AppData\\Local\\Google\\Chrome\\User Data");
            //chromeOptions.AddArgument("profile-directory=Profile 1");

            try
            {
                driver = new ChromeDriver(driverService, chromeOptions);
            }
            catch (WebDriverException ex)
            {
                try
                {
                    var chromeDriverProcesses = Process.GetProcesses().Where(pr => pr.ProcessName.Contains("chrome"));
                    foreach (var process in chromeDriverProcesses)
                    {
                        process.Kill();
                    }
                    driver = new ChromeDriver(driverService, chromeOptions);
                }
                catch(Exception lastex)
                {
                    MessageBox.Show("Please End All running Chrome instances and try again!");
                    if (driver !=null) driver.Quit();
                    return;
                }

            }

            try
            {
                driver.Navigate().GoToUrl("https://parler.com/auth/access");

                driver.FindElement(By.Id("wc--2--login")).Click();
                driver.FindElement(By.Id("mat-input-0")).SendKeys(txt_Useremail.Text);
                driver.FindElement(By.Id("mat-input-1")).SendKeys(txt_Password.Text);

                var div = driver.FindElement(By.Id("auth-form--actions"));
                var btns = div.FindElements(By.TagName("button"));
                btns[0].Click();

            }
            catch (Exception ex)
            {

            }

            while (true)
            {
                if (driver != null)
                {
                    try
                    {
                        if (driver.Url != null && driver.Url.Contains("parler.com/feed"))
                        {
                            btn_Login.Enabled = false;
                            btn_Start.Enabled = true;
                            break;
                        }
                        else
                        {
                            Thread.Sleep(1000);
                        }
                    }
                    catch (Exception ex)
                    {
                        break;
                    }

                }
            }

        }
        private void btn_Savesetting_Click(object sender, EventArgs e)
        {
            AddUpdateAppSettings("follow_Keyword", txt_Keyword.Text);
        }
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {

            if (upvoter !=null && upvoter.IsAlive)
            {
                upvoter.Join();
            }
            if (follower != null && follower.IsAlive)
            {
                follower.Join();
            }
            if (driver != null)
            {
                driver.Quit();
            }
            RegistryKey rkey = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\PalerSettings");
            rkey.SetValue("NumberofRun", NumberofRun);

        }

        private void btn_Start_Click(object sender, EventArgs e)
        {
            if (!KeyRegistered)            
            {
                var m2 = new  Form2();
                m2.ShowDialog();
            }

            if (driver == null || driver.Url == "") return;
            if (driver.WindowHandles.Count > 1) return;

            if (string.IsNullOrEmpty(txt_Keyword.Text))
            {
                MessageBox.Show("Please Fill out the Setting values");
                return;
            }

            lblcurrentIndex.Text = "0";
            keyword = txt_Keyword.Text;

            //follower = new Thread(Follow);
            upvoter = new Thread(Upvote);

            btn_Login.Enabled = false;
            btn_Start.Enabled = false;
            btn_Pause.Enabled = true;
            btn_Stop.Enabled = true;

            _pauseEvent.Reset();
            _stopEvent.Reset();
            isRunning = true;


            //follower.Start();
            upvoter.Start();

        }

        private void DisplayRemainingTime(string element, string value)
        {
            switch (element)
            {
                case "lblcurrentIndex":
                    lblcurrentIndex.Text = value;
                    break;
                case "lblupvotes":
                    lblupvotes.Text = value;
                    break;
                default:
                    break;
            }

        }
        private void Upvote()
        {

            try
            {
                if (string.IsNullOrEmpty(keyword)) keyword = "Trump";
                driver.Navigate().GoToUrl("https://parler.com/search?searchTerm=" + keyword);
                var wait = new WebDriverWait(driver, TimeSpan.FromMinutes(1));

                wait.Until(ExpectedConditions.ElementIsVisible(By.CssSelector("span.user-result--wrapper")));
                bool exitflag = false;

                var body = driver.FindElement(By.TagName("body"));
                var peoples = driver.FindElements(By.CssSelector("span.user-result--wrapper"));
                

                var js = ((IJavaScriptExecutor)driver);
                bool checkLastone = true;

                displayTimer = new Timer(1000);
                displayTimer.Elapsed += displayTimer_callback;
                displayTimer.AutoReset = true;
                displayTimer.Enabled = true;


                _pauseEvent.Set();
                for (int i = 0; i < peoples.Count; i++)
                {
                    _pauseEvent.WaitOne(Timeout.Infinite);
                    if (_stopEvent.WaitOne(0))
                    {
                        exitflag = true; break;
                    }


                    if (exitflag) break;
                    var follow = peoples[i].FindElement(By.CssSelector("button[id^=\"action-button--width--follow\"]"));
                    var link = peoples[i].FindElement(By.CssSelector("a.username")).GetAttribute("href");
                    var name = peoples[i].FindElement(By.CssSelector("a.name")).Text;


                    try
                    {
                        if (checkLastone)
                        {
                            int j = peoples.Count - 1;
                            string temp = peoples[j].FindElement(By.CssSelector("button[id^=\"action-button--width--follow\"]")).Text;
                            if (temp == "Following")
                            {
                                i = peoples.Count - 1;
                                js.ExecuteScript("arguments[0].scrollIntoView(true);", follow);
                                driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);
                            }
                            checkLastone = false;
                        }
                        if (i == peoples.Count - 1)
                        {
                            peoples = driver.FindElements(By.CssSelector("span.user-result--wrapper"));
                            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);
                            js.ExecuteScript("arguments[0].scrollIntoView(true);", follow);
                            checkLastone = true;
                        }
                        var outputDelegate = new OutputDelegate(DisplayRemainingTime);

                        string disName = string.Format("{0} , {1}th people", name, i + 1);
                        this.Invoke(outputDelegate, "lblcurrentIndex", disName);

                        var res = js.ExecuteScript("arguments[0].scrollIntoView(true);", follow);

                        //if (txt != "Following") js.ExecuteScript("arguments[0].click();", follow);
                        js.ExecuteScript("window.open();");
                        driver.SwitchTo().Window(driver.WindowHandles.Last());
                        driver.Navigate().GoToUrl(link);

                        var wait15 = new WebDriverWait(driver, TimeSpan.FromSeconds(15));
                        wait15.Until(ExpectedConditions.ElementIsVisible(By.CssSelector("div#post-action-component--wrapper")));
                        var posts = driver.FindElements(By.CssSelector("div#post-action-component--wrapper"));

                        for (int j = 0; j<posts.Count; j++)
                        {
                            try
                            {
                                _pauseEvent.WaitOne(Timeout.Infinite);
                                if (_stopEvent.WaitOne(0))
                                {
                                    exitflag = true; break;
                                }

                                var post_comment = posts[j].FindElement(By.CssSelector("lib-i-upvote"));
                                string fill = post_comment.FindElement(By.TagName("svg")).GetAttribute("fill");
                                if (fill == "#67a3c1") continue;

                                if (exitflag) break;
                                if (j == posts.Count - 1)
                                {
                                    posts = driver.FindElements(By.CssSelector("div#post-action-component--wrapper"));
                                    driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);
                                }


                                var commentsIcon = posts[j].FindElement(By.CssSelector("lib-i-comments"));
                                js.ExecuteScript("arguments[0].scrollIntoView(true);", commentsIcon);
                                js.ExecuteScript("arguments[0].click();", post_comment);
                                js.ExecuteScript("arguments[0].click();", commentsIcon);

                                wait.Until(ExpectedConditions.ElementIsVisible(By.CssSelector(".comment-actions--wrapper")));
                                var comments = posts[j].FindElements(By.CssSelector(".comment-actions--wrapper"));
                                for (int k = 0; k < comments.Count; k++)
                                {
                                    if (exitflag) break;
                                    _pauseEvent.WaitOne(Timeout.Infinite);
                                    if (_stopEvent.WaitOne(0))
                                    {
                                        exitflag = true; break;
                                    }

                                    if (k == comments.Count - 1)
                                    {
                                        comments = posts[j].FindElements(By.CssSelector(".comment-actions--wrapper"));
                                        driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);
                                    }
                                    var voteicon = comments[k].FindElement(By.CssSelector("div.pointer:nth-child(3) lib-i-upvote"));
                                    js.ExecuteScript("arguments[0].scrollIntoView(true);", voteicon);
                                    js.ExecuteScript("arguments[0].click();", voteicon);
                                    totalvotes++;
                                }
                            }
                            catch(Exception postex)
                            {

                            }

                        }


                    }
                    catch(Exception pex)
                    {

                    }
                    js.ExecuteScript("window.close();");
                    driver.SwitchTo().Window(driver.WindowHandles.First());
                }
            }
            catch(Exception uex)
            {
                MessageBox.Show(uex.Message.ToString());
            }
        }
        private void btn_Pause_Click(object sender, EventArgs e)
        {
            if (isRunning)
            {
                _pauseEvent.Reset();
                isRunning = false;
                btn_Pause.Text = "Resume";

            }
            else
            {
                _pauseEvent.Set();
                isRunning = true;
                btn_Pause.Text = "Pause";
                
            }
        }

        private void displayTimer_callback(Object source, ElapsedEventArgs e)
        {
            try
            {
                var outputDelegate = new OutputDelegate(DisplayRemainingTime);
                this.Invoke(outputDelegate, "lblupvotes", totalvotes.ToString());

            }
            catch(Exception ex)
            {

            }


        }
        public List<Browser> GetBrowsers()
        {
            List<Browser> browsers = new List<Browser>();
            RegistryKey browserKeys;
            //on 64bit the browsers are in a different location
            browserKeys = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Clients\StartMenuInternet");
            if (browserKeys == null)
                browserKeys = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Clients\StartMenuInternet");
            string[] browserNames = browserKeys.GetSubKeyNames();

            for (int i = 0; i < browserNames.Length; i++)
            {
                Browser browser = new Browser();
                RegistryKey browserKey = browserKeys.OpenSubKey(browserNames[i]);
                browser.Name = (string)browserKey.GetValue(null);
                RegistryKey browserKeyPath = browserKey.OpenSubKey(@"shell\open\command");
                browser.Path = (string)browserKeyPath.GetValue(null).ToString().Trim();
                RegistryKey browserIconPath = browserKey.OpenSubKey(@"DefaultIcon");
                browser.IconPath = (string)browserIconPath.GetValue(null).ToString().Trim();
                browsers.Add(browser);
                if (browser.Path != null)
                    browser.Version = FileVersionInfo.GetVersionInfo(browser.Path.Replace("\"", "")).FileVersion;
                else
                    browser.Version = "unknown";
            }
            return browsers;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (follower != null && follower.IsAlive)
            {
                follower.Join();
            }
            if (driver != null)
            {
                driver.Quit();
            }
            RegistryKey rkey = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\PalerSettings");
            rkey.SetValue("NumberofRun", NumberofRun);
            this.Close();
        }

        public string ReadSetting(string key)
        {
            string result = "";
            try
            {
                var appSettings = ConfigurationManager.AppSettings;
                result = appSettings[key] ?? "0";

            }
            catch (ConfigurationErrorsException)
            {

            }
            return result;
        }
        public void AddUpdateAppSettings(string key, string value)
        {
            try
            {
                var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                var settings = configFile.AppSettings.Settings;
                if (settings[key] == null)
                {
                    settings.Add(key, value);
                }
                else
                {
                    settings[key].Value = value;
                }
                configFile.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection(configFile.AppSettings.SectionInformation.Name);
            }
            catch (ConfigurationErrorsException)
            {

            }
        }



        private void btn_Stop_Click(object sender, EventArgs e)
        {
            isRunning = false;
            _stopEvent.Set();
            _pauseEvent.Set();


            btn_Start.Enabled = true;
            btn_Pause.Enabled = false;
            btn_Stop.Enabled = false;

            if (upvoter.IsAlive) upvoter.Join();

        }
    }
}
public class Browser
{
    public string Name { get; set; }
    public string Path { get; set; }
    public string IconPath { get; set; }
    public string Version { get; set; }
}
