using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Xml;
using HtmlDocument = HtmlAgilityPack.HtmlDocument;
using MessageBox = System.Windows.Forms.MessageBox;

namespace ITCubeRG
{
    internal class Program : INotifyPropertyChanged
    {
        private readonly string Pattern = @"^--- \/ OR\/\d{4}\/\d{2}\/\d{5}$|OF\/\d{4}\/\d{2}\/\d{5} \/ OR\/\d{4}\/\d{2}\/\d{5}$";
        public string Login { get; set; }
        public string Password { get; set; }
        public string Month { get; set; }
        public int Year { get; set; }
        public double ExchangeRateEur { get; set; }
        public double ExchangeRateGbp { get; set; }
        public string PathToSave { get; set; }
        private AccessToken token;
        private DateTimeFormatInfo dtfi = new CultureInfo("en-US").DateTimeFormat;
        public event Action<int> ProgressChanged;
        public event PropertyChangedEventHandler PropertyChanged;
        private int startId;
        private int endId;
        private string progressText;
        public string ProgressText
        {
            get { return progressText; }
            set
            {
                if (progressText != value)
                {
                    progressText = value;
                    OnPropertyChanged(nameof(ProgressText));
                }
            }
        }

        public Program()
        {
        }

        public async Task StartAsync()
        {
            Logger.Logger.Log.Info($"The program starts working with the following parameters Login = {Login}, Year = {Year}, Month = {Month}, Path = {PathToSave}");
            var sw = new Stopwatch();
            //await Console.Out.WriteLineAsync("The program starts creating report with following parameters:");
            //SessionId = ExtractJSessionId(ConfigurationManager.AppSettings["Token"]);
            //await Console.Out.WriteLineAsync($"Year of report: {ConfigurationManager.AppSettings["Year"]}");
            //await Console.Out.WriteLineAsync($"Month of report: {ConfigurationManager.AppSettings["Month"]}");
            //await Console.Out.WriteLineAsync($"Place to save report : {ConfigurationManager.AppSettings["PathToSaveReport"]}");
            switch (Year)
            {
                case 2022:
                    {
                        startId = 12500;
                        endId = 16000;
                        break;
                    }
                case 2023:
                    {
                        startId = 15000;
                        endId = 18000;
                        break;
                    }
                case 2024:
                    {
                        startId = 17000;
                        endId = 21000;
                        break;
                    }
                default:
                    {
                        startId = 0;
                        endId = 20000;
                        break;
                    }

            }
            token = await getToken();

            sw.Start();
            List<string> resultList = await Task.Run(async () => await Generate(startId, endId));
            sw.Stop();
            if (resultList.Count != 0)
            {
                Logger.Logger.Log.Info($"Report was generated for {sw.ElapsedMilliseconds} ms");
                await WriteToFile(resultList);
            }
            else
            {
                Logger.Logger.Log.Error("There were no data that meet the parameters");
                throw new Exception("There is no data meet the parameters");
            }
        }

        private async Task<List<string>> Request(int id)
        {

            List<string> resultOfOneId = new List<string>();
            string numberOfOrder = "";
            DateTime dateOfOrder;
            string url = $"http://crm.logopakeast.pl:8080/crm/Jsp/viewOrder.jsp;jsessionid={token.SessionId}?command=viewOrder&nextPage=viewOrder.jsp&mode=view&OrderId={id}";
            var handler = new HttpClientHandler();
            handler.CookieContainer = new System.Net.CookieContainer();
            handler.CookieContainer.Add(new Uri(url), new System.Net.Cookie("ITCubeSessionId_0_18", token.Cookies));
            using (HttpClient client = new HttpClient(handler))
            {
                try
                {
                    HttpResponseMessage response = await client.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        string htmlContent = await response.Content.ReadAsStringAsync();
                        HtmlDocument htmlDoc = new HtmlDocument();
                        htmlDoc.LoadHtml(htmlContent);
                        HtmlNode bodyNode = htmlDoc.DocumentNode.SelectSingleNode("//body");
                        string text = bodyNode.InnerText.Trim();
                        if (text.Length != 0)
                        {
                            // Find the number of order
                            HtmlNodeCollection paragraphs = htmlDoc.DocumentNode.SelectNodes("//td[contains(b, 'Nr oferty/zamówienia: ')]/following-sibling::td");
                            if (paragraphs != null)
                            {
                                foreach (HtmlNode paragraph in paragraphs)
                                {
                                    numberOfOrder = paragraph.InnerText.ToString().Trim();
                                }
                                // Find date of the order
                                HtmlNodeCollection paragraphs2 = htmlDoc.DocumentNode.SelectNodes("//td[contains(b, 'Złożenie/Sprzedaż: ')]/following-sibling::td");
                                if (Regex.IsMatch(numberOfOrder, Pattern) && paragraphs2 != null)
                                {
                                    foreach (HtmlNode paragraph in paragraphs2)
                                    {
                                        string dateTimeString = paragraph.InnerText.ToString().Trim().Substring(0, 10).Trim();
                                        if (DateTime.TryParseExact(dateTimeString.ToString(), "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out dateOfOrder))
                                        {
                                            DateTime date = DateTime.ParseExact(Month, "MMMM", dtfi);
                                            if (dateOfOrder.Year == Year && dateOfOrder.Month == date.Month)
                                            {
                                                //Going to lines
                                                List<string> nameOfProducts = GetNameOfProductList(htmlDoc);
                                                List<string> nettoPrices = GetNettoPriceList(htmlDoc);
                                                List<string> currencyList = GetCurrencyList(htmlDoc);
                                                List<string> category = GetCategoryList(nameOfProducts);
                                                double pricePLN;

                                                if (nameOfProducts.Count == nettoPrices.Count && nettoPrices.Count == currencyList.Count)
                                                {
                                                    int count = nameOfProducts.Count;
                                                    StringBuilder stringBuilder = new StringBuilder();
                                                    for (int i = 0; i < count; i++)
                                                    {
                                                        if (currencyList[i].Equals("PLN"))
                                                        {
                                                            pricePLN = Convert.ToDouble(nettoPrices[i]);
                                                        }
                                                        else if (currencyList[i].Equals("EUR"))
                                                        {

                                                            pricePLN = Math.Round(Convert.ToDouble(nettoPrices[i]) * ExchangeRateEur, 2);

                                                        }
                                                        else if (currencyList[i].Equals("GBP"))
                                                        {
                                                            pricePLN = Math.Round(Convert.ToDouble(nettoPrices[i]) * ExchangeRateGbp, 2);
                                                        }
                                                        else
                                                            pricePLN = 0;
                                                        stringBuilder.Append(numberOfOrder).Append(";").Append(dateOfOrder.ToString("yyyy-MM-dd")).Append(";").Append(nameOfProducts[i]).Append(";").Append(nettoPrices[i]).Append(";").Append(currencyList[i]).Append(";").Append(pricePLN).Append(";").Append(category[i]).Append(";").Append(id);
                                                        resultOfOneId.Add(stringBuilder.ToString());
                                                        stringBuilder.Clear();
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            Logger.Logger.Log.Error("Access accessToken is not correct");
                            throw new Exception("Access accessToken is not correct");
                        }
                    }
                    else
                    {
                        Logger.Logger.Log.Error($"Response code is not success: {response.StatusCode} - {response.ReasonPhrase}");
                        throw new Exception($"Response code is not success: {response.StatusCode} - {response.ReasonPhrase}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Logger.Log.Error("Error during internet connection" + ex.ToString());
                    throw new Exception("Error during internet connection");
                }
            }
            return resultOfOneId;
        }

        private List<string> GetCategoryList(List<string> nameOfProducts)
        {
            List<string> result = new List<string>();
            foreach (var item in nameOfProducts)
            {
                if (item.StartsWith("Etykiet") || item.StartsWith("Taśma") || item.StartsWith("Winietka") || item.StartsWith("Kalka") || item.StartsWith("Tasma") || item.StartsWith("TTR"))
                {
                    result.Add("Consumables");
                }
                else if (item.StartsWith("Midmeki") || item.StartsWith("Logopak") || item.StartsWith("Drukarka") || item.StartsWith("Meki") || item.Contains("Logomatic") || item.Contains("Maszyna") || item.StartsWith("Detektor"))
                {
                    result.Add("Machine");
                }
                else if (item.StartsWith("Print") || item.StartsWith("Moduł") || item.StartsWith("Usługa") || item.StartsWith("Roboczogodzina") || item.StartsWith("Dojazd") || item.StartsWith("Płyt")
                     || item.StartsWith("dojazd") || item.StartsWith("Wizyta") || item.StartsWith("wizyta") || item.StartsWith("Płyn") || item.StartsWith("Nocleg") || item.Contains("Filter") || item.Contains("Belt") || StartsWithDigit(item)
                     || item.StartsWith("dostawa") || item.StartsWith("Dostawa") || item.StartsWith("Diet") || item.StartsWith("Hotel") || item.StartsWith("Kamera") || item.StartsWith("opakowanie") || item.StartsWith("Transport")
                     || item.StartsWith("Travel") || item.StartsWith("usługa") || item.StartsWith("Work") || item.StartsWith("Zestaw") || item.StartsWith("Tooth") || item.StartsWith("Tester") || item.StartsWith("S") || item.StartsWith("s") || item.Contains("motor")
                     || item.StartsWith("instalacja") || item.StartsWith("Bulb") || item.StartsWith("Bateria") || item.Contains("Rubber") || item.Contains("Rolka") || item.StartsWith("Cost") || item.Contains("switch") || item.Contains("gumowa") || item.Contains("Belt") || item.Contains("traveling") || item.Contains("Pasek")
                     || item.Contains("socket") || item.Contains("roller") || item.Contains("Bearing") || item.Contains("Łożysko") || item.Contains("Fotokomórka") || item.StartsWith("Delivery") || item.Contains("Głowica") || item.Contains("EPROM")
                     || item.Contains("Patyczki") || item.Contains("Roundbelt") || item.Contains("LogoCare") || item.Contains("LogoClean") || item.Contains("electronic") || item.StartsWith("Mod")
                     || item.Contains("Rurka") || item.Contains("Terminal") || item.Contains("Instalacja") || item.Contains("travel") || item.Contains("Sensor") || item.Contains("BATTERY") || item.Contains("mocujący") || item.Contains("Battery") || item.Contains("adapter") || item.Contains("adapter") || item.Contains("Supply") || item.Contains("CPU") || item.Contains("Motor"))
                {
                    result.Add("Service");
                }
                else
                {
                    result.Add("");
                }
            }
            return result;
        }

        private bool StartsWithDigit(string item)
        {
            // Проверяем, не пустая ли строка и начинается ли она с цифры
            return !string.IsNullOrEmpty(item) && char.IsDigit(item.FirstOrDefault());
        }

        private async Task<List<string>> Generate(int startId, int endId)
        {
            Stopwatch sw = new Stopwatch();
            Logger.Logger.Log.Info("The program started generating report");
            List<string> list = new List<string>();
            for (int i = startId; i <= endId; i++)
            {
                sw.Start();
                if (i % 100 == 0)
                {
                    sw.Stop();
                    int timeLeft = ((int)sw.Elapsed.TotalSeconds * ((endId - i) / 100));
                    ProgressText = $"Completed {i} records from {endId}, time left = " + (timeLeft == 0 ? ".." : timeLeft.ToString()) + " seconds ";
                    sw.Restart();
                }
                OnProgressChanged(((i - startId) * 100) / (endId - startId));
                List<string> tempList = await Request(i);
                list.AddRange(tempList);
            }
            return list;
        }

        private List<string> GetNameOfProductList(HtmlDocument htmlDoc)
        {
            var tdNodes = htmlDoc.DocumentNode.SelectNodes($"//tr[@class='tablelistitem']//td[@class='maindata titledata']//a");
            List<string> values = new List<string>();
            if (tdNodes != null)
            {
                foreach (var tdNode in tdNodes)
                {
                    values.Add(RemoveParagraphs(tdNode.InnerText.Trim()).Replace(';', ','));
                }
            }
            return values;
        }

        private List<string> GetNettoPriceList(HtmlDocument htmlDoc)
        {
            var tdNodes = htmlDoc.DocumentNode.SelectNodes($"//tr[@class='tablelistitem']//td[@valign='top'][position() = 11]");
            List<string> values = new List<string>();
            if (tdNodes != null)
            {
                foreach (var tdNode in tdNodes)
                {
                    string str = tdNode.InnerText.Trim();
                    values.Add(str.Replace(',', ' ').Replace('.', ','));
                }
            }
            return values;
        }

        private List<string> GetCurrencyList(HtmlDocument htmlDoc)
        {
            var tdNodes = htmlDoc.DocumentNode.SelectNodes("//tr[@class='tablelistitem']//td[@valign='top'][position() = 14]");
            List<string> values = new List<string>();
            if (tdNodes != null)
            {
                foreach (var tdNode in tdNodes)
                {
                    string str = tdNode.InnerText.Trim();
                    values.Add(str);
                }
            }
            return values;
        }


        private async Task WriteToFile(List<string> lines)
        {
            string fileName = $"{DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")}.txt";
            if (string.IsNullOrEmpty(PathToSave))
            {
                PathToSave = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,fileName);
            }
            else
            {
                PathToSave = Path.Combine(PathToSave, fileName);
                

            }
            using (FileStream stream = new FileStream(PathToSave, FileMode.OpenOrCreate))
            {
                foreach (string line in lines)
                {
                    await stream.WriteAsync(Encoding.Default.GetBytes(line), 0, Encoding.Default.GetByteCount(line));
                    await stream.WriteAsync(Encoding.Default.GetBytes("\n"), 0, 1);
                }
            }
            Logger.Logger.Log.Info($"The report successfuly saved to {PathToSave}");
            System.Windows.Forms.MessageBox.Show($"Done! The report successfuly saved to {PathToSave}");
        }
        private string RemoveParagraphs(string input)
        {
            // Замена символов новой строки на пустую строку
            string result = input.Replace("\n", "");
            result = result.Replace("\r", "");
            result = result.Replace("\t", "");
            return result;
        }

        private async Task<AccessToken> getToken()
        {
            string cookies = "";
            string sessionId = "";
            string url = @"http://crm.logopakeast.pl:8080/crm/Jsp/commandCenterAction.jsp";
            using (HttpClient client = new HttpClient())
            {
                var formContent = new FormUrlEncodedContent(new[]
                {
                new KeyValuePair<string, string>("command", "login"),
                new KeyValuePair<string, string>("nextPage", "welcomeFrame.jsp"),
                new KeyValuePair<string, string>("login",Login ),
                new KeyValuePair<string, string>("password", Password),
                new KeyValuePair<string, string>("isSW", ""),
                new KeyValuePair<string, string>("isExplorer", "0"),
                new KeyValuePair<string, string>("isExplorer10up", "0"),
                new KeyValuePair<string, string>("isFirefox", "0"),
                new KeyValuePair<string, string>("isSafari", "1"),
                new KeyValuePair<string, string>("isMobile", "0"),
                new KeyValuePair<string, string>("isiPad", "0"),
                new KeyValuePair<string, string>("viewPort", ""),
                new KeyValuePair<string, string>("submited.x", "66"),
                new KeyValuePair<string, string>("submited.y", "9"),
                new KeyValuePair<string, string>("LoginInternalConnection", "1"),
                new KeyValuePair<string, string>("LoginReload", "1"),
            });
                HttpResponseMessage response;
                try
                {
                    response = await client.PostAsync(url, formContent);
                }
                catch (Exception ex)
                {
                    Logger.Logger.Log.Error("There is no internet connection " + ex.ToString());
                    throw new Exception($"There is no internet connection");
                }
                string responseBody = await response.Content.ReadAsStringAsync();
                responseBody = responseBody.Trim();
                int startIndexSessionId = responseBody.IndexOf("jsessionid=") + "jsessionid=".Length;
                int endIndexSessionId = responseBody.IndexOf("?Param=True", startIndexSessionId);
                int startIndexCookies = responseBody.IndexOf("ITCubeSessionId_0_18=") + "ITCubeSessionId_0_18=".Length;
                int endIndexCookies = responseBody.IndexOf("\";window", startIndexCookies);

                if (startIndexSessionId >= 0 && endIndexSessionId >= 0 && startIndexCookies >= 0 && endIndexCookies >= 0)
                {
                    sessionId = responseBody.Substring(startIndexSessionId, endIndexSessionId - startIndexSessionId);
                    cookies = responseBody.Substring(startIndexCookies, endIndexCookies - startIndexCookies);
                    Logger.Logger.Log.Info("Log in successfuly");
                }
                else
                {
                    Logger.Logger.Log.Info("Session id is not found. Login or password are not correct !!! ");
                    throw new Exception("Session id is not found. Login or password are not correct !!! ");
                }
            }
            AccessToken accessToken = new AccessToken(cookies, sessionId);

            return accessToken;
        }
        protected virtual void OnProgressChanged(int value)
        {
            // Вызов события в основном потоке UI
            App.Current.Dispatcher.Invoke(() => ProgressChanged?.Invoke(value));
        }
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }
}
