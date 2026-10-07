using System.Threading.Channels;
using Inventory.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace Inventory.Infrastructure.Correo;

// Cola en memoria entre quien genera el aviso y el servicio que lo envía. Apagado el correo, Encolar no hace nada.
// Si se reinicia la API, lo que estaba en cola se pierde: es aceptable porque la campanita sigue teniendo el aviso.
public class ColaCorreo : ICorreoSaliente
{
    private readonly Channel<MensajeCorreo> _canal = Channel.CreateBounded<MensajeCorreo>(new BoundedChannelOptions(500)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
    });
    private readonly bool _habilitado;

    public ColaCorreo(IOptions<CorreoOptions> opciones) => _habilitado = opciones.Value.Habilitado;

    public ChannelReader<MensajeCorreo> Lector => _canal.Reader;

    public void Encolar(MensajeCorreo mensaje)
    {
        if (_habilitado) _canal.Writer.TryWrite(mensaje);
    }
}
