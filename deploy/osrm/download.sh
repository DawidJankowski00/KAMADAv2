#!/bin/sh
# Downloads the OpenStreetMap extract for the self-hosted OSRM container (once – the volume keeps it).
# Default: Kujawsko-Pomorskie voivodeship (Bydgoszcz, ~100 MB) from Geofabrik; override with OSRM_PBF_URL.
set -eu

if [ -f /data/region.osm.pbf ]; then
    echo "OSM extract already downloaded."
    exit 0
fi

echo "Downloading ${OSRM_PBF_URL} ..."
curl -fL --retry 3 -o /data/region.osm.pbf.part "${OSRM_PBF_URL}"
mv /data/region.osm.pbf.part /data/region.osm.pbf
