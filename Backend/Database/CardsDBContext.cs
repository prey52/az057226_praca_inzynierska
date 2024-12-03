
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;


namespace Backend.Database
{
	public class CardsDBContext : IdentityDbContext<DBUser>
	{
		public CardsDBContext(DbContextOptions<CardsDBContext> options)
		: base(options)
		{
		}

		// DbSets for your entities
		public DbSet<Deck> Decks { get; set; }
		public DbSet<Card> Cards { get; set; }

		protected override void OnModelCreating(ModelBuilder builder)
		{
			base.OnModelCreating(builder);

			// Deck to User relationship
			builder.Entity<Deck>()
				.HasOne(d => d.User)
				.WithMany(u => u.Decks)
				.HasForeignKey(d => d.UserId)
				.OnDelete(DeleteBehavior.Cascade);

			// Card to Deck relationship
			builder.Entity<Card>()
				.HasOne(c => c.Deck)
				.WithMany(d => d.Cards)
				.HasForeignKey(c => c.DeckId)
				.OnDelete(DeleteBehavior.Cascade);
		}
	}
}
