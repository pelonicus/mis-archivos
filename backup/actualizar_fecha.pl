use strict;
use warnings;
use POSIX qw(strftime);

my $m3u_file = shift or die "Uso: $0 archivo.m3u\n";

# Obtener fecha actual en formato "28 Feb. 2026 12:12"
my $date_str = strftime("%d %b. %Y %H:%M", localtime);

# Leer todo el archivo
open(my $in, "<", $m3u_file) or die "No puedo abrir $m3u_file\n";
my @lines = <$in>;
close($in);

# Modificar la primera línea que tenga tvg-id="update_time"
my $found = 0;
for my $line (@lines) {
    if (!$found && $line =~ /tvg-id="update_time"/) {
        $line =~ s/,(.*)$/,$date_str/;
        $found = 1;
        last; # solo la primera
    }
}

# Sobrescribir el archivo
open(my $out, ">", $m3u_file) or die "No puedo escribir $m3u_file\n";
print $out @lines;
close($out);

print "Fecha actualizada a: $date_str\n";