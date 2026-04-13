use strict;
use warnings;

my $input_file = shift or die "Uso: $0 archivo_grande.xml\n";
my $MAX_SIZE = 49 * 1024 * 1024; # 50MB

open(my $in, "<", $input_file) or die "No puedo abrir $input_file\n";
my $content = do { local $/; <$in> };
close($in);

$content =~ s/\r//g;
$content =~ s/\n\s+/ /g;

# Extraer channels
my @channels;
while ($content =~ m{(<channel\b.*?</channel>)}gs) {
    push @channels, $1;
}

# Extraer programmes
my @programmes;
while ($content =~ m{(<programme\b.*?</programme>)}gs) {
    push @programmes, $1;
}

# Mapear programmes por canal
my %prog_by_channel;
foreach my $p (@programmes) {
    if ($p =~ /channel="([^"]+)"/) {
        push @{ $prog_by_channel{$1} }, $p;
    }
}

# Crear archivos por tamaño
my $file_index = 1;
my $current_size = 0;
my @current_channels;
my @current_programmes;

sub write_file {
    my ($idx, $chs_ref, $prgs_ref) = @_;
    my $fname = "epgshare$idx.xml";
    open(my $out, ">", $fname) or die "No puedo crear $fname\n";
    print $out qq{<?xml version="1.0" encoding="UTF-8"?>\n<tv>\n};
    print $out "$_\n" for @$chs_ref;
    print $out "$_\n" for @$prgs_ref;
    print $out "</tv>\n";
    close($out);
    print "Generado $fname con " . scalar(@$chs_ref) . " canales.\n";
}

for my $ch (@channels) {
    my ($id) = $ch =~ /id="([^"]+)"/;
    next unless $id;
    my @progs = @{ $prog_by_channel{$id} || [] };

    my $block_size = length($ch) + length(join('', @progs));

    # Si el archivo actual supera MAX_SIZE, escribir y reset
    if ($current_size + $block_size > $MAX_SIZE && @current_channels) {
        write_file($file_index++, \@current_channels, \@current_programmes);
        @current_channels = ();
        @current_programmes = ();
        $current_size = 0;
    }

    push @current_channels, $ch;
    push @current_programmes, @progs;
    $current_size += $block_size;
}

# Escribir último archivo si queda algo
if (@current_channels) {
    write_file($file_index++, \@current_channels, \@current_programmes);
}