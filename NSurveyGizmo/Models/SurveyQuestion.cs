﻿using System.CodeDom;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Newtonsoft.Json;

namespace NSurveyGizmo.Models
{
    public class SurveyQuestion
    {
        [Key]
        public int id { get; set; }
        public int page { get; set; }
        public LocalizableString title { get; set; }
        [JsonProperty("type")]
        public string _subtype { get; set; }
        public string value { get; set; }
        public string question { get; set; }
        [JsonProperty("base_type")]
        public string _type { get; set; }
        public string shortName { get; set; }
        public string answer { get; set; }
        public int section_id { get; set; }
        public int? answer_id { get; set; }
        public bool shown { get; set; }
        public string QuestionResponse { get; set; }
        public QuestionProperties properties { get; set; }
        public QuestionOptions[] options { get; set; }

        public bool Equals(SurveyQuestion sq)
        {
            return id               == sq.id
                && page       == sq.page
                && _subtype         == sq._subtype
                && _type            == sq._type
                && shortName        == sq.shortName
                && QuestionResponse == sq.QuestionResponse
                && title.Equals(sq.title)
                && properties.Equals(sq.properties)
                && options.SequenceEqual(sq.options);
        }
    }

    public class QuestionProperties
    {
        public bool option_sort { get; set; }
        public bool required { get; set; }
        public bool hidden { get; set; }
        public string orientation { get; set; }
        public LocalizableString question_description { get; set; }

        public bool Equals(QuestionProperties qp)
        {
            return option_sort == qp.option_sort
                && required    == qp.required
                && hidden      == qp.hidden
                && orientation == qp.orientation
                && question_description.Equals(qp.question_description);
        }
    }

    public class QuestionOptions
    {
        [Key]
        public int id { get; set; }
        public string shortName { get; set; }
        public string option { get; set; }
        public string answer { get; set; }
        public LocalizableString title { get; set; }


        public bool Equals(QuestionOptions qo)
        {
            return id == qo.id && title.Equals(qo.title);
        }
    }
}
