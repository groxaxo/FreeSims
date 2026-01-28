using System.Collections.Generic;
using System.Runtime.Serialization;

namespace FSO.Client.LLM
{
    [DataContract]
    public sealed class LLMInteractionInfo
    {
        [DataMember(Name = "id")]
        public int id;
        
        [DataMember(Name = "name")]
        public string name;
    }

    [DataContract]
    public sealed class LLMSimState
    {
        [DataMember(Name = "sim_name")]
        public string sim_name;
        
        [DataMember(Name = "motives")]
        public Dictionary<string, int> motives = new Dictionary<string, int>();
        
        [DataMember(Name = "nearby_objects")]
        public List<LLMObjectInfo> nearby_objects = new List<LLMObjectInfo>();
        
        [DataMember(Name = "recent_chat")]
        public List<string> recent_chat = new List<string>();
        
        [DataMember(Name = "current_action")]
        public string current_action;
    }

    [DataContract]
    public sealed class LLMObjectInfo
    {
        [DataMember(Name = "guid")]
        public string guid;
        
        [DataMember(Name = "name")]
        public string name;
        
        [DataMember(Name = "interactions")]
        public List<LLMInteractionInfo> interactions = new List<LLMInteractionInfo>();
        
        [DataMember(Name = "distance")]
        public float distance;
    }

    [DataContract]
    public sealed class LLMAgentResponse
    {
        [DataMember(Name = "action_type")]
        public string action_type;
        
        [DataMember(Name = "target_guid")]
        public string target_guid;
        
        [DataMember(Name = "interaction_id")]
        public int? interaction_id;
        
        [DataMember(Name = "speech_text")]
        public string speech_text;
        
        [DataMember(Name = "thought_process")]
        public string thought_process;
    }
}
