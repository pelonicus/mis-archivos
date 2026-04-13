use strict;
use warnings;

my $file = shift or die "Uso: $0 archivo.xml\n";

open(my $in, "<", $file) or die "No puedo abrir $file\n";
my $content = do { local $/; <$in> };
close($in);

# eliminar saltos de linea
$content =~ s/\r//g;
$content =~ s/\n\s+/ /g;

# Contar programas por canal
my %programme_count;
while ($content =~ m{<programme\b.*?channel="([^"]+)".*?</programme>}gs) {
    my $id = $1;
    $programme_count{$id}++;
}

# Modificar display-name de cada channel
$content =~ s{(<channel\b.*?id="([^"]+)".*?>.*?<display-name[^>]*>)(.*?)(</display-name>)}{
    my ($start, $id, $name, $end) = ($1, $2, $3, $4);
    my $count = $programme_count{$id} || 0;
    "$start$name($count)$end";
}egs;

# Sobrescribir archivo original
open(my $out, ">", $file) or die "No puedo escribir $file\n";
print $out $content;
close($out);

print "Archivo $file actualizado con conteo de programaciones.\n";