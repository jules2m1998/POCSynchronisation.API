using Mediator.Abstractions;
using Poc.Synchronisation.Domain.Abstractions;
using Poc.Synchronisation.Domain.Events.Packages;
using Poc.Synchronisation.Domain.Models;

namespace POCSync.MAUI
{
    public partial class MainPage : ContentPage
    {
        private readonly IEmitter _emitter;
        private readonly IBaseRepository<Package, Guid> _repo;
        int count = 0;

        public MainPage(IEmitter emitter, IBaseRepository<Package, Guid> repo)
        {
            InitializeComponent();
            _emitter = emitter;
            _repo = repo;
        }

        private void OnCounterClicked(object sender, EventArgs e)
        {
            count++;

            if (count == 1)
                CounterBtn.Text = $"Clicked {count} time";
            else
                CounterBtn.Text = $"Clicked {count} times";
            _ = test();

            SemanticScreenReader.Announce(CounterBtn.Text);
        }

        private async Task test()
        {
            Package package = new()
            {
                Id = Guid.CreateVersion7(),
                Reference = "REF-123456",
                Weight = 10.5m,
                Volume = 0.5m,
                TareWeight = 1.0m,
            };
            var result = await _emitter.EmitAsync(new CreatePackageEvent
            {
                Data = package,
                EmitedOn = DateTime.UtcNow,
            });

            var data = await _repo.GetAllAsync();
            var r = data.FirstOrDefault();
        }
    }

}
