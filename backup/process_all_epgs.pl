#!/usr/bin/perl
use strict;
use warnings;
use IO::Uncompress::Gunzip qw(gunzip $GunzipError);
use File::Glob ':glob';

my $m3u_file    = 'personal.m3u';
my $output_file = 'all_epgshare.xml';

print "Leyendo M3U...\n";

# ----------------- Leer y limpiar nombres de M3U -----------------
open(my $m3u, "<", $m3u_file) or die "No puedo abrir $m3u_file\n";

my %wanted;
while (my $line = <$m3u>) {
    next unless $line =~ /^#EXTINF/i;
    if ($line =~ /,(.+)$/) {
        my $name = lc($1);
        # limpiar paréntesis y números
        $name =~ s/\([^)]*\)//g;
        $name =~ s/\d+//g;
        $name =~ s/\s+/ /g;
        $name =~ s/^\s+|\s+$//g;
        next unless length $name;
        $wanted{$name} = 1;
    }
}
close($m3u);
print "Canales buscados: " . scalar(keys %wanted) . "\n\n";

# ----------------- Procesar archivos XML.GZ -----------------
my @gz_files = bsd_glob("*.xml.gz");
die "No se encontraron archivos .xml.gz\n" unless @gz_files;

my %channels_data;      # id => { block, country }
my %programme_count;    # id => cantidad
my @all_programmes;

foreach my $gz_file (@gz_files) {
    print "Procesando $gz_file ...\n";

    # extraer código país del archivo (archivo.PAIS.xml.gz)
    my ($country) = $gz_file =~ /\.([A-Z0-9]{2,3})\.xml\.gz$/i;
    $country ||= "XX";

    my $z = IO::Uncompress::Gunzip->new($gz_file)
        or do {
            print "Error en $gz_file: $GunzipError\n";
            next;
        };

    my %valid_ids;

    while (defined(my $line = <$z>)) {

        # ----------------- CHANNEL -----------------
        while ($line =~ /(<channel\b.*?<\/channel>)/gs) {
            my $block = $1;

            # obtener id
            my ($id) = $block =~ /id="([^"]+)"/;

            # obtener display-name
            my ($display) = $block =~ /<display-name[^>]*>(.*?)<\/display-name>/i;
            next unless $display;

            # limpiar nombre para coincidencia
            my $xml_name = lc($display);
            $xml_name =~ s/\([^)]*\)//g;
            $xml_name =~ s/\d+//g;
            $xml_name =~ s/\s+/ /g;
            $xml_name =~ s/^\s+|\s+$//g;

            foreach my $wanted_name (keys %wanted) {
                if (index($xml_name, $wanted_name) != -1
                    || index($wanted_name, $xml_name) != -1) {

                    unless (exists $channels_data{$id}) {
                        $block =~ s/\r?\n/ /g;
                        $block =~ s/\s{2,}/ /g;
                        $channels_data{$id} = {
                            block   => $block,
                            country => $country,
                        };
                    }
                    $valid_ids{$id} = 1;
                    last;
                }
            }
        }

        # ----------------- PROGRAMME -----------------
        while ($line =~ /(<programme\b.*?<\/programme>)/gs) {
            my $block = $1;

            # actualizar zona horaria a +0000
            $block =~ s/(start="[\d]+) [^"]+"/$1 +0000"/;
            $block =~ s/(stop="[\d]+) [^"]+"/$1 +0000"/;

            my ($id) = $block =~ /channel="([^"]+)"/;
            if ($id && exists $valid_ids{$id}) {
                $block =~ s/\r?\n/ /g;
                $block =~ s/\s{2,}/ /g;
                push @all_programmes, $block;
                $programme_count{$id}++;
            }
        }
    }

    close($z);
}

# ----------------- GENERAR XML FINAL -----------------
print "\nGenerando $output_file ...\n";

open(my $out, ">", $output_file) or die "No puedo escribir $output_file\n";

print $out qq{<?xml version="1.0" encoding="UTF-8"?>\n};
print $out qq{<!DOCTYPE tv SYSTEM "xmltv.dtd">\n};
print $out qq{<tv>\n};

foreach my $id (keys %channels_data) {
    my $block   = $channels_data{$id}{block};
    my $country = $channels_data{$id}{country};
    my $count   = $programme_count{$id} || 0;

    $block =~ s{(<display-name[^>]*>)(.*?)(</display-name>)}{
        my $name = $2;
        "$1$name($country)($count)$3"
    }e;

    # separar cualquier channel múltiple en líneas
    $block =~ s/<channel/\n<channel/g;
    $block =~ s/^\n//;

    print $out "$block\n";
}

foreach my $prog (@all_programmes) {
    print $out "$prog\n";
}

print $out "</tv>\n";
close($out);

print "====================================\n";
print "Archivo generado: $output_file\n";
print "Canales: " . scalar(keys %channels_data) . "\n";
print "Programas: " . scalar(@all_programmes) . "\n";
print "Proceso terminado.\n";