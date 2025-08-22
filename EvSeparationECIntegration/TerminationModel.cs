using System;
using System.Configuration;
using Newtonsoft.Json;
namespace EvSeparationECIntegration
{
	/*
	 SAMPLE PAYLOAD
		{
			"__metadata":{
				   "uri":"EmpEmploymentTermination"
			},
			"personIdExternal":"1234567",
			"userId":"1234567",
			"endDate":"/Date(1558556800000)/",
			"eventReason":"NO_SHOW_EVENT_REASON"
		}
	*/
	public class TokenInfo
	{
		public string Token { get; set; }
		public DateTime ExpiresAt { get; set; }
	}

	public class Metadata
	{
		[JsonProperty("uri")]
		public string Uri { get; set; }
	}
	public class TerminationData
	{
		public int ResignationId { get; set; }
		public string SFUserId { get; set; }
		public string DASID { get; set; }
		public DateTime LWD { get; set; }
		public string EventReason { get; set; }
	}

	public class TerminationPayload
	{
		[JsonProperty("__metadata")]
		public Metadata Metadata { get; set; }

		[JsonProperty("personIdExternal")]
		public string PersonIdExternal { get; set; }

		[JsonProperty("userId")]
		public string UserId { get; set; }

		[JsonProperty("endDate")]
		public string EndDate { get; set; }  // Keep as string for /Date(...) format

		[JsonProperty("eventReason")]
		public string EventReason { get; set; }

		//Method to serialize itself to JSON
		public string ToJson()
		{
			return JsonConvert.SerializeObject(this);
		}

		// static method to build payload object
		public static TerminationPayload Create(string personId, string userId, DateTime endDate, string eventReason, string terminationUri)
		{
            //Reference for calculating millis, Shared by Vishnu on 09-JUL-2025
            //https://currentmillis.com/

            /*  (1) Below logic has been failed hence commented. Example: It converts Date 30-JUL-2025 00:00 To 29-JUL-2025 18:30 and then Millis, which is incorrect
			  
				long millis = new DateTimeOffset(endDate.Date.ToUniversalTime()).ToUnixTimeMilliseconds();
				long millis = new DateTimeOffset(endDate.Date).ToUnixTimeMilliseconds();
				
				(2) Another logic was to add hardcoded 5.30 hours and then convert, which got rejected due to the hardcoded part.
				
				endDate = endDate.AddHours(5.5);
				long unixMilliseconds = new DateTimeOffset(endDate).ToUnixTimeMilliseconds();

				For testing there is a commented Main function on Program.cs class.

				Below is the best suitable logic, since it doesn't have any hardcoded value. and it will function correctly irrespective of system's Time Zone
			*/
            DateTime utcDate = new DateTime(endDate.Year, endDate.Month, endDate.Day, 0, 0, 0, DateTimeKind.Utc);
            long millis = new DateTimeOffset(utcDate).ToUnixTimeMilliseconds();

            return new TerminationPayload
			{
				Metadata = new Metadata { Uri = terminationUri },
				PersonIdExternal = personId,
				UserId = userId,
				EndDate = $"/Date({millis})/",
				EventReason = eventReason
			};
		}
	}

	public class SeparationECStatusLogData
	{
		public string CompanyArea { get; set; }
		public string DasId { get; set; }
		public bool IsSuccess { get; set; }
		public string RequestContent { get; set; }
		public string ResponseContent { get; set; }
		public string ErrorMessage { get; set; }
		public DateTime StartTime { get; set; }
		public DateTime EndTime { get; set; }
		public Guid RunId { get; set; }
		public string ErrorDetails { get; set; }
        public string ErrorSendTo { get; set; }
    }


}
