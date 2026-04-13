#!/usr/bin/perl
use strict;
use warnings;

# ----------------- Argumentos -----------------
my ($file, $key) = @ARGV;

die "Uso: delete_code.pl archivo.m3u TEXTO_CLAVE\n" unless $file && $key;

# ----------------- Leer archivo -----------------
open(my $in, "<", $file) or die "No puedo abrir $file: $!";

my @output;
my $skip_next = 0;
my $deleted = 0;

while (my $line = <$in>) {

    # Si la linea anterior fue eliminada (#EXTINF), saltamos la URL
    if ($skip_next) {
        $skip_next = 0;
        next;
    }

    # Detectar EXTINF con (CLAVE)
    if ($line =~ /^#EXTINF:.*\($key\)/) {
        $skip_next = 1;   # saltar tambien la siguiente linea (URL)
        $deleted++;       # contar canal eliminado
        next;
    }

    push @output, $line;
}

close($in);

# ----------------- Sobrescribir archivo -----------------
open(my $out, ">", $file) or die "No puedo escribir $file: $!";

print $out @output;

close($out);

print "Canales con ($key) eliminados: $deleted\n";
print "Archivo actualizado: $file\n";