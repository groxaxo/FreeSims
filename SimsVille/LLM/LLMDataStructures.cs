using System.Collections.Generic;
using System.Runtime.Serialization;

namespace FSO.Client.LLM
{
    [DataContract]
    public sealed class LLMInteractionInfo
    {
        [DataMember(Name = "id")]
        public int Id;
        
        [DataMember(Name = "name")]
        public string Name;
    }

    [DataContract]
    public sealed class LLMSimState
    {
        [DataMember(Name = "sim_name")]
        public string SimName;
        
        [DataMember(Name = "motives")]
        public Dictionary<string, int> Motives = new Dictionary<string, int>();
        
        [DataMember(Name = "nearby_objects")]
        public List<LLMObjectInfo> NearbyObjects = new List<LLMObjectInfo>();
        
        [DataMember(Name = "recent_chat")]
        public List<string> RecentChat = new List<string>();
        
        [DataMember(Name = "current_action")]
        public string CurrentAction;
    }

    [DataContract]
    public sealed class LLMObjectInfo
    {
        [DataMember(Name = "guid")]
        public string Guid;
        
        [DataMember(Name = "name")]
        public string Name;
        
        [DataMember(Name = "interactions")]
        public List<LLMInteractionInfo> Interactions = new List<LLMInteractionInfo>();
        
        [DataMember(Name = "distance")]
        public float Distance;
    }

    [DataContract]
    public sealed class LLMAgentResponse
    {
        [DataMember(Name = "action_type")]
        public string ActionType;
        
        [DataMember(Name = "target_guid")]
        public string TargetGuid;
        
        [DataMember(Name = "interaction_id")]
        public int? InteractionId;
        
        [DataMember(Name = "speech_text")]
        public string SpeechText;
        
        [DataMember(Name = "thought_process")]
        public string ThoughtProcess;
    }
}
