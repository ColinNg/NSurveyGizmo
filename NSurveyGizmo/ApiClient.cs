﻿using Newtonsoft.Json;
using NLog;
using Polly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace NSurveyGizmo
{
    public class ApiClient
    {
        public IThrottledWebRequest ThrottledWebRequest = new ThrottledWebRequest();
        public int? BatchSize = null;
        public string BaseServiceUrl = "https://restapi.surveygizmo.com/v4/";
        public string ApiToken { get; set; }
        public string ApiTokenSecret { get; set; }
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        #region questions

        public List<SurveyQuestion> GetQuestions(int surveyId, bool getAllPages = true)
        {
            return GetData<SurveyQuestion>("survey/" + surveyId + "/surveyquestion", getAllPages, true);
        }

        #endregion

        #region responses

        public List<SurveyResponse> GetResponses(int surveyId, bool getAllPages = true)
        {
            return GetData<SurveyResponse>("survey/" + surveyId + "/surveyresponse", getAllPages, true);
        }

        #endregion

        #region surveys

        public List<Survey> GetAllSurveys(bool getAllPages = true)
        {
            return GetData<Survey>("survey", getAllPages, true);
        }

        public int CreateSurvey(string title)
        {
            var surveys = GetData<Survey>("survey/?_method=PUT&type=survey&title=" + Uri.EscapeDataString(title ?? ""));
            // TODO: return the survey object?
            if (surveys == null || surveys.Count < 1) return 0;
            return surveys[0].id;
        }

        public bool DeleteSurvey(int surveyId)
        {
            var results = GetData<Result>("survey/" + surveyId + "?_method=DELETE", nonQuery: true);
            return ResultOk(results);
        }

        public Survey GetSurvey(int id)
        {
            var results = GetData<Survey>("survey/" + id);
            return results != null && results.Count > 0 ? results[0] : null;
        }

        #endregion

        #region campaigns

        public int CreateCampaign(int surveyId, string campaignName, int masterCampaignId = 0)
        {
            var method = "PUT";
            var id = "";
            var type = "&type=email";
            var copy = "";

            if (masterCampaignId > 0)
            {
                method = "POST";
                id = "/" + masterCampaignId;
                type = "";
                copy = "&copy=true";
            }

            var campaigns = GetData<SurveyCampaign>("survey/" + surveyId + "/surveycampaign" + id + "?_method=" + method + type + "&name=" +
                                  Uri.EscapeDataString(campaignName ?? "") + copy);

            // TODO: return campaign object?

            if (campaigns == null || campaigns.Count < 1) return 0;
            return campaigns[0].id;
        }

        public List<SurveyCampaign> GetCampaigns(int surveyId, bool getAllPages = true)
        {
            return GetData<SurveyCampaign>("survey/" + surveyId + "/surveycampaign", getAllPages, true);
        }

        public SurveyCampaign GetCampaign(int surveyId, int campaignId)
        {
            var results = GetData<SurveyCampaign>("survey/" + surveyId + "/surveycampaign/" + campaignId);
            return results != null && results.Count > 0 ? results[0] : null;
        }

        public bool DeleteCampaign(int surveyId, int campaignId)
        {
            var results = GetData<Result>("survey/" + surveyId + "/surveycampaign/" + campaignId + "?_method=DELETE", nonQuery: true);
            return ResultOk(results);
        }

        public bool UpdateCampaign(int surveyId, SurveyCampaign campaign)
        {
            var url = BuildUrl("survey/" + surveyId + "/surveycampaign/" + campaign.id + "?_method=POST",
                new Dictionary<string, string>() { { "name", campaign.name }, { "status", campaign.status } });

            // TODO: allow updating the rest of the properties of a campaign

            var results = GetData<Result>(url.ToString(), nonQuery: true);

            return ResultOk(results);
        }

        #endregion

        #region email messages

        public List<EmailMessage> GetEmailMessageList(int surveyId, int campaignId)
        {
            return GetData<EmailMessage>("survey/" + surveyId + "/surveycampaign/" + campaignId + "/emailmessage", true, true);
        }

        public bool UpdateEmailMessage(int surveyId, int campaignId, EmailMessage emailMessage)
        {
            var url =
                new StringBuilder("survey/" + surveyId + "/surveycampaign/" + campaignId + "/emailmessage/" +
                                  emailMessage.id + "?_method=POST");

            if (emailMessage.from != null)
            {
                if (emailMessage.from.name != null)
                {
                    url.Append("&from[name]=" + Uri.EscapeDataString(emailMessage.from.name));
                }

                if (emailMessage.from.email != null)
                {
                    url.Append("&from[email]=" + Uri.EscapeDataString(emailMessage.from.email));
                }
            }

            // TODO: allow updating of the rest of the properties of an email message

            var results = GetData<Result>(url.ToString(), nonQuery: true);
            return ResultOk(results);
        }

        #endregion

        #region contacts

        public int CreateContact(int surveyId, int campaignId, Contact contact)
        {
            var url = BuildCreateOrUpdateContactUrl(surveyId, campaignId, contact, true);
            var results = GetData<Result>(url, nonQuery: true);
            if (results == null || results.Count < 1 || results[0].result_ok == false) return -1;
            return results[0].id;
        }

        public bool UpdateContact(int surveyId, int campaignId, Contact contact)
        {
            var url = BuildCreateOrUpdateContactUrl(surveyId, campaignId, contact);
            var results = GetData<Result>(url, nonQuery: true);
            return ResultOk(results);
        }

        private string BuildCreateOrUpdateContactUrl(int surveyId, int campaignId, Contact contact, bool isNewContact = false)
        {
            var method       = isNewContact ? "PUT" : "POST";
            var strContactId = isNewContact ? "" : contact.ID.ToString();
            var baseUrl      = "survey/" + surveyId + "/surveycampaign/" + campaignId + "/contact/" + strContactId + "?_method=" + method;

            var requiredParams    = new[] { nameof(contact.Email), nameof(contact.FirstName), nameof(contact.LastName), nameof(contact.Organization) };
            var parameters        = new Dictionary<string, string>();
            var contactProperties = contact.GetType().GetProperties().Where(pi => pi.Name != nameof(contact.ID)).ToList();

            foreach (var propertyInfo in contactProperties)
            {
                var value = propertyInfo.GetValue(contact, null).ToString();
                if (string.IsNullOrEmpty(value) && !requiredParams.Contains(propertyInfo.Name)) continue;

                var jsonPropertyObj  = propertyInfo.GetCustomAttributes(true).FirstOrDefault(o => o is JsonPropertyAttribute);
                var jsonPropertyAttr = jsonPropertyObj as JsonPropertyAttribute;
                var name = jsonPropertyAttr != null ? jsonPropertyAttr.PropertyName : propertyInfo.Name;

                parameters.Add(name, value);
            }

            return BuildUrl(baseUrl, parameters).ToString();
        }

        [Obsolete("Use CreateContact(surveyId, campaignId, contact), instead.")]
        public int CreateContact(int surveyId, int campaignId, string emailAddress = null,
            string firstName = null, string lastName = null, string organization = null, params string[] customFields)
        {
            var url = BuildCreateOrUpdateContactUrl(surveyId, campaignId, null, emailAddress, firstName, lastName, organization, customFields);
            var results = GetData<Result>(url, nonQuery: true);
            if (results == null || results.Count < 1 || results[0].result_ok == false) return -1;
            return results[0].id;
        }

        [Obsolete("Use UpdateContact(surveyId, campaignId, contact), instead.")]
        public bool UpdateContact(int surveyId, int campaignId, int contactId, string emailAddress = null,
            string firstName = null, string lastName = null, string organization = null, params string[] customFields)
        {
            var url = BuildCreateOrUpdateContactUrl(surveyId, campaignId, contactId, emailAddress, firstName, lastName,
                organization, customFields);
            var results = GetData<Result>(url, nonQuery: true);
            return ResultOk(results);
        }

        [Obsolete("Use BuildCreateOrUpdateContactUrl(surveyId, campaignId, contact, isNewContact), instead.")]
        private string BuildCreateOrUpdateContactUrl(int surveyId, int campaignId, int? contactId, string emailAddress = null, string firstName = null, string lastName = null, string organization = null, params string[] customFields)
        {
            var method       = contactId == null ? "PUT" : "POST";
            var strContactId = contactId.HasValue ? contactId.ToString() : "";
            var baseUrl      = "survey/" + surveyId + "/surveycampaign/" + campaignId + "/contact/" + strContactId + "?_method=" + method;
            

            var url = BuildUrl(baseUrl,
                    new Dictionary<string, string>()
                    {
                        {"semailaddress", emailAddress},
                        {"sfirstname", firstName},
                        {"slastname", lastName},
                        {"sorganization", organization}
                    });

            for (var i = 0; i < customFields.Length; i++)
            {
                if (customFields[i] != null)
                {
                    url.Append("&scustomfield" + (i + 1) + "=" + Uri.EscapeDataString(customFields[i]));
                }
            }

            return url.ToString();
        }

        public bool DeleteContact(int surveyId, int campaignId, int contactId)
        {
            var results = GetData<Result>("survey/" + surveyId + "/surveycampaign/" + campaignId + "/contact/" + contactId +
                      "?_method=DELETE", nonQuery: true);
            return ResultOk(results);
        }
        #endregion

        #region contact lists
        public List<Contact> GetCampaignContactList(int surveyId, int campaignId)
        {
            return GetData<Contact>("survey/" + surveyId + "/surveycampaign/" + campaignId + "/contact", true, true);
        }

        public bool UpdateContactList(int contactListId, string email, string firstName, string lastName, string organization, Dictionary<string, string> customFields)
        {
            var url =
                BuildUrl("contactlist/" + contactListId + "?_method=POST&semailaddress=" + Uri.EscapeDataString(email),
                    new Dictionary<string, string>()
                    {
                        {"sfirstname", firstName},
                        {"slastname", lastName},
                        {"sorganization", organization}
                    });

            foreach (var key in customFields.Keys)
            {
                if (customFields[key] == null) continue;
                url.Append("&custom[" + key + "]=" + Uri.EscapeDataString(customFields[key]));
            }

            var results = GetData<Result>(url.ToString(), nonQuery: true);
            return ResultOk(results);
        }

        #endregion

        private bool ResultOk(List<Result> results)
        {
            // might want to return the result object instead of a bool
            return results.Count > 0 && results[0].result_ok;
        }

        private StringBuilder BuildUrl(string baseUrl, Dictionary<string, string> parameters)
        {
            var url = new StringBuilder(baseUrl);

            foreach (var parameter in parameters.Where(parameter => parameter.Key != null && parameter.Value != null))
            {
                url.Append("&" + parameter.Key + "=" + Uri.EscapeDataString(parameter.Value));
            }

            return url;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="url"></param>
        /// <param name="getAllPages"></param>
        /// <param name="paged"></param>
        /// <param name="nonQuery">If this parameter is true, the method returns a list with just one Result object that will indicate success/failure of the API call.</param>
        /// <returns></returns>
        private List<T> GetData<T>(string url, bool getAllPages = false, bool paged = false, bool nonQuery = false)
        {
            // TODO: use async?

            var fullUrl = BaseServiceUrl + url + (url.Contains("?") ? "&" : "?") + "api_token=" + ApiToken + "&api_token_secret=" + ApiTokenSecret;
            var currentUrl = fullUrl;
            var page = 1;
            var totalPages = 1;

            if (paged && BatchSize != null && BatchSize > 0)
            {
                fullUrl += "&resultsperpage=" + BatchSize;
            }

            var policy = Policy
                .Handle<WebException>()
                .Retry(10, (ex, i) =>
                {
                    var exception = ex as WebException;
                    if (exception != null)
                    {
                        SetNLogContextItems(exception, currentUrl);
                        Logger.Log(LogLevel.Error, exception, exception.Message);
                    }

                    if (i > 10)
                    {
                        throw new Exception(ex.Message, ex);
                    }
                });

            var data = new List<T>();

            policy.Execute(() =>
            {
                if (!paged)
                {
                    if (nonQuery)
                    {
                        var result1 = ThrottledWebRequest.GetJsonObject<T>(fullUrl);
                        data.Add(result1);
                        return;
                    }

                    var result = ThrottledWebRequest.GetJsonObject<Result<T>>(fullUrl);
                    data.Add(result.Data);
                    return;
                }

                for (; page <= totalPages && totalPages != 0; page++)
                {
                    var pagedUrl = fullUrl + "&page=" + page;
                    currentUrl = pagedUrl;

                    var result = ThrottledWebRequest.GetJsonObject<PagedResult<T>>(pagedUrl);

                    if (!result.result_ok)
                    {
                        break;
                    }

                    if (getAllPages)
                    {
                        totalPages = result.total_pages;
                    }

                    if (result.Data != null)
                    {
                        data.AddRange(result.Data.Where(d => d != null));
                    }
                }
            });

            return data;
        }

        private static int? GetStatusCode(WebException webException)
        {
            if (webException.Status != WebExceptionStatus.ProtocolError) return null;

            var response = webException.Response as HttpWebResponse;
            if (response != null)
            {
                return (int)response.StatusCode;
            }

            return null;
        }

        private void SetNLogContextItems(WebException webException, string url)
        {
            GlobalDiagnosticsContext.Set("apiUrl", url);
            GlobalDiagnosticsContext.Set("httpStatusCode", GetStatusCode(webException).ToString());
        }
    }
}
