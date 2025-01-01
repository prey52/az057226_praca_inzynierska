using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;


namespace Backend.Classes.Database
{
    public class CardsDBContext : IdentityDbContext<DBUser>
    {
        public CardsDBContext(DbContextOptions<CardsDBContext> options)
        : base(options)
        {
        }

        // DbSets for your entities
        public DbSet<DBUser> User { get; set; }
        public DbSet<QuestionDeck> QuestionDecks { get; set; }
        public DbSet<AnswerDeck> AnswerDecks { get; set; }
        public DbSet<QuestionCard> QuestionCards { get; set; }
        public DbSet<AnswerCard> AnswerCards { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<DBUser>()
                .HasMany(u => u.QuestionDecks)
                .WithOne(q => q.User)
                .HasForeignKey(q => q.UserId);

            modelBuilder.Entity<DBUser>()
                .HasMany(u => u.AnswerDecks)
                .WithOne(a => a.User)
                .HasForeignKey(a => a.UserId);

            modelBuilder.Entity<QuestionDeck>()
                .HasMany(q => q.QuestionCards)
                .WithOne(qc => qc.QuestionDeck)
                .HasForeignKey(qc => qc.QuestionDeckId);

            modelBuilder.Entity<AnswerDeck>()
                .HasMany(a => a.AnswerCards)
                .WithOne(ac => ac.AnswerDeck)
                .HasForeignKey(ac => ac.AnswerDeckId);
        }
    }
}
