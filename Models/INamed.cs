namespace MediaArchive.Models;

// Lookup tables keyed by a human-typed name, resolved pick-or-create.
public interface INamed
{
    string Name { get; set; }
}
