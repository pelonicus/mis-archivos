#!/usr/bin/perl
use strict;
use warnings;
use IO::Uncompress::Gunzip qw(gunzip $GunzipError);
use File::Glob ':glob';

my $m3u_file    = 'personal.m3u';
my $output_file = 'all_epgshare.xml';

print "Leyendo M3U...\n";

# ----------------- Leer nombres del M3U -----------------

open(my $m3u, "<", $m3u_file) or die "No puedo abrir $m3u_file\n";

my %wanted_full;
my %wanted_first;

while (my $line = <$m3u>) {

    next unless $line =~ /^#EXTINF/i;

    if ($line =~ /,(.+)$/) {

        my $name = lc($1);

        $name =~ s/\([^)]*\)//g;
        $name =~ s/\s+/ /g;
        $name =~ s/^\s+|\s+$//g;

        next unless length $name;

        $wanted_full{$name} = 1;

        my ($first) = split(/\s+/, $name);
        $wanted_first{$first} = 1 if $first;
    }
}

close($m3u);

print "Canales buscados: " . scalar(keys %wanted_full) . "\n\n";

# ----------------- Procesar XML.GZ -----------------

my @gz_files = bsd_glob("*.xml.gz");
die "No se encontraron archivos .xml.gz\n" unless @gz_files;

my %channels_data;
my %programme_count;
my @all_programmes;

foreach my $gz_file (@gz_files) {

    print "Procesando $gz_file ...\n";

    my ($country) = $gz_file =~ /\.([A-Z0-9]{2,3})\.xml\.gz$/i;
    $country ||= "XX";

    my $z = IO::Uncompress::Gunzip->new($gz_file)
        or do {
            print "Error en $gz_file: $GunzipError\n";
            next;
        };

    my $xml = '';
    my $buffer;

    while ($z->read($buffer) > 0) {
        $xml .= $buffer;
    }

    close($z);

    my %valid_ids;

    # ----------------- CHANNEL -----------------

    while ($xml =~ /(<channel\b.*?<\/channel>)/gis) {

        my $block = $1;

        my ($id) = $block =~ /id="([^"]+)"/i;
        my ($display) = $block =~ /<display-name[^>]*>(.*?)<\/display-name>/i;

        next unless $display;

        my $xml_name = lc($display);

        $xml_name =~ s/\([^)]*\)//g;
        $xml_name =~ s/\s+/ /g;
        $xml_name =~ s/^\s+|\s+$//g;

        my ($first_word) = split(/\s+/, $xml_name);

        my $match = 0;

        foreach my $wanted (keys %wanted_full) {

            if (index($xml_name, $wanted) != -1) {
                $match = 1;
                last;
            }
        }

        if (!$match && exists $wanted_first{$first_word}) {
            $match = 1;
        }

        if ($match) {

            unless (exists $channels_data{$id}) {

                $block =~ s/\r?\n/ /g;
                $block =~ s/\s{2,}/ /g;

                $channels_data{$id} = {
                    block   => $block,
                    country => $country,
                };
            }

            $valid_ids{$id} = 1;
        }
    }

    # ----------------- PROGRAMME -----------------

    while ($xml =~ /(<programme\b.*?<\/programme>)/gis) {

        my $block = $1;

        my ($id) = $block =~ /channel="([^"]+)"/i;

        if ($id && exists $valid_ids{$id}) {

            $block =~ s/\r?\n/ /g;
            $block =~ s/\s{2,}/ /g;

            push @all_programmes, $block;

            $programme_count{$id}++;
        }
    }
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

    # ---- CORREGIR ESPACIOS EN channel id ----
    1 while $block =~ s/(<channel[^>]*id="[^"]*)\s([^"]*")/$1.$2/g;

    $block =~ s/<channel/\n<channel/g;
    $block =~ s/^\n//;

    print $out "$block\n";
}

foreach my $prog (@all_programmes) {

    # ---- CORREGIR ESPACIOS EN programme channel ----
    1 while $prog =~ s/(<programme[^>]*channel="[^"]*)\s([^"]*")/$1.$2/g;

    print $out "$prog\n";
}

print $out "</tv>\n";

close($out);

print "====================================\n";
print "Archivo generado: $output_file\n";
print "Canales: " . scalar(keys %channels_data) . "\n";
print "Programas: " . scalar(@all_programmes) . "\n";
print "Proceso terminado.\n";