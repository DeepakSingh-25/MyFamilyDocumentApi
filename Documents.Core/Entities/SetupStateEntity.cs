namespace Documents.Core.Entities
{
    /// <summary>
    /// One-row guard: row maujood = sign-up band. Id hamesha 1 hai, isliye do saath-saath sign-up
    /// mein se doosra primary key violation se fail hota hai.
    /// </summary>
    public class SetupStateEntity
    {
        public const byte SingletonId = 1;

        public byte Id { get; set; } = SingletonId;
        public DateTime ClosedOn { get; set; }
        public Guid ClosedByUserId { get; set; }
        public UserEntity ClosedByUser { get; set; } = null!;
    }
}
