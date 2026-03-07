#!/usr/bin/perl
use strict;
use warnings;
use File::Copy;
use POSIX qw(strftime);

# ----------------- Argumentos -----------------
my $pattern = shift or die "Uso: $0 <texto_en_parentesis>\n";
my $file    = 'personal.m3u';

# ----------------- Crear backup -----------------
mkdir "backup" unless -d "backup";
my $timestamp = strftime("%Y%m%d_%H%M%S", localtime);
my $backup_file = "backup/$file.$timestamp";
copy($file, $backup_file) or die "No se pudo crear backup $backup_file: $!\n";
print "Backup creado: $backup_file\n";

# ----------------- Procesar archivo -----------------
open(my $in,  "<", $file) or die "No puedo abrir $file\n";
my @lines = <$in>;
close($in);

my @new_lines;
my $skip_next = 0;

for (my $i = 0; $i < @lines; $i++) {
    my $line = $lines[$i];

    if ($skip_next) {
        $skip_next = 0;
        next;  # saltar la URL del canal
    }

    if ($line =~ /\($pattern\)/) {
        print "Eliminando canal: $line";
        $skip_next = 1; # saltar siguiente línea
        next;
    }

    push @new_lines, $line;
}

# ----------------- Sobrescribir archivo -----------------
open(my $out, ">", $file) or die "No puedo escribir $file\n";
print $out @new_lines;
close($out);

print "Proceso terminado. Se eliminaron todas las coincidencias de '$pattern'.\n";