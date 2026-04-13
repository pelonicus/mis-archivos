use strict;
use warnings;
use LWP::UserAgent;
use File::Basename;

# ============================
# LISTA DE URLS
# ============================

my @urls = (

    'https://epgshare01.online/epgshare01/epg_ripper_CA2.xml.gz',
    'https://epgshare01.online/epgshare01/epg_ripper_US2.xml.gz',
    'https://epgshare01.online/epgshare01/epg_ripper_MX1.xml.gz',
    'https://epgshare01.online/epgshare01/epg_ripper_SV1.xml.gz',
    'https://epgshare01.online/epgshare01/epg_ripper_CO1.xml.gz',
    'https://epgshare01.online/epgshare01/epg_ripper_PE1.xml.gz',
    'https://epgshare01.online/epgshare01/epg_ripper_AR1.xml.gz',
    'https://epgshare01.online/epgshare01/epg_ripper_CH1.xml.gz',
    'https://epgshare01.online/epgshare01/epg_ripper_ES1.xml.gz',

    'https://www.open-epg.com/files/canada1.xml.gz',
    'https://www.open-epg.com/files/canada2.xml.gz',
    'https://www.open-epg.com/files/canada3.xml.gz',
    'https://www.open-epg.com/files/canada4.xml.gz',
    'https://www.open-epg.com/files/canada5.xml.gz',
    'https://www.open-epg.com/files/canada6.xml.gz',
    'https://www.open-epg.com/files/canada7.xml.gz',

    'https://www.open-epg.com/files/unitedstates3.xml.gz',
    'https://www.open-epg.com/files/unitedstates4.xml.gz',
    'https://www.open-epg.com/files/unitedstates6.xml.gz',
    'https://www.open-epg.com/files/unitedstates7.xml.gz',
    'https://www.open-epg.com/files/unitedstates8.xml.gz',
    'https://www.open-epg.com/files/unitedstates9.xml.gz',
    'https://www.open-epg.com/files/unitedstates10.xml.gz',
    'https://www.open-epg.com/files/unitedstates11.xml.gz',

    'https://www.open-epg.com/files/mexico1.xml.gz',
    'https://www.open-epg.com/files/mexico2.xml.gz',

    'https://www.open-epg.com/files/elsalvador1.xml.gz',

    'https://www.open-epg.com/files/colombia1.xml.gz',
    'https://www.open-epg.com/files/colombia2.xml.gz',

    'https://www.open-epg.com/files/peru1.xml.gz',
    'https://www.open-epg.com/files/peru2.xml.gz',
    'https://www.open-epg.com/files/peru3.xml.gz',

    'https://www.open-epg.com/files/argentina1.xml.gz',
    'https://www.open-epg.com/files/argentina2.xml.gz',
    'https://www.open-epg.com/files/argentina3.xml.gz',
    'https://www.open-epg.com/files/argentina4.xml.gz',
    'https://www.open-epg.com/files/argentina5.xml.gz',


    'https://www.open-epg.com/files/chile1.xml.gz',
    'https://www.open-epg.com/files/chile2.xml.gz',

    'https://www.open-epg.com/files/spain1.xml.gz',
    'https://www.open-epg.com/files/spain2.xml.gz',
    'https://www.open-epg.com/files/spain3.xml.gz',
    'https://www.open-epg.com/files/spain4.xml.gz',
    'https://www.open-epg.com/files/spain5.xml.gz',
    'https://www.open-epg.com/files/spain6.xml.gz',
    'https://www.open-epg.com/files/spain7.xml.gz',

    'https://www.open-epg.com/files/sports1.xml.gz',
    'https://www.open-epg.com/files/sports2.xml.gz',
    'https://www.open-epg.com/files/sports5.xml.gz',

);

# ============================
# MAPEO DE NOMBRES DE PAISES
# ============================

my %country_names = (

    canada        => 'CA',
    unitedstates  => 'US',
    mexico        => 'MX',
    elsalvador    => 'SV',
    colombia      => 'CO',
    peru          => 'PE',
    argentina     => 'AR',
    chile         => 'CL',
    spain         => 'ES',

);

# ============================

my $ua = LWP::UserAgent->new(

    agent   => 'Mozilla/5.0 (Windows NT 10.0; Win64; x64)',
    timeout => 60,

);

$ua->max_redirect(5);

foreach my $url (@urls) {

    print "Descargando: $url ... ";

    my $response = $ua->get($url);

    unless ($response->is_success) {

        print "FAIL (" . $response->status_line . ")\n";
        next;
    }

    my $filename = basename($url);
    my $country_code = '';

    # ============================
    # Detectar país desde epg_ripper
    # ============================

    if ($filename =~ /epg_ripper_([A-Z]{2})/i) {

        $country_code = uc($1);

    }

    # ============================
    # Detectar país desde nombre
    # ============================

    else {

        foreach my $country (keys %country_names) {

            if ($filename =~ /$country/i) {

                $country_code = $country_names{$country};
                last;

            }
        }
    }

    # ============================
    # Si no detecta país
    # ============================

    $country_code ||= "XX";

    # ============================
    # Agregar código al nombre
    # ============================

    $filename =~ s/\.xml\.gz$/\.$country_code.xml.gz/;

    open(my $fh, ">", $filename)
        or die "No se pudo guardar $filename\n";

    binmode($fh);
    print $fh $response->content;
    close($fh);

    print "OK -> $filename\n";
}

print "\nProceso terminado.\n";