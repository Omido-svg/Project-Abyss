namespace ProjectAbyss.Abilities
{
    /// <summary>
    /// R6-Ia execution trace. The scheduler's dequeue trace records INTENT;
    /// this record describes what the combat coordinator actually executed.
    /// </summary>
    public readonly struct R6ReactionExecutionRecord
    {
        public readonly ReactionRequest Request;
        public readonly long ExecutedActionId;
        public readonly bool Executed;
        public readonly string Diagnostic;
        public R6ReactionExecutionRecord(ReactionRequest request,long actionId,
            bool executed,string diagnostic)
        {
            Request=request;
            ExecutedActionId=actionId;
            Executed=executed;
            Diagnostic=diagnostic??string.Empty;
        }
    }
}
